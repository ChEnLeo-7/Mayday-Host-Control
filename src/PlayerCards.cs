using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppMayday.UI;
using Il2CppMayday;
using Il2CppMirror;
using Il2CppSteamworks;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MaydayHostControl
{
    public sealed partial class Plugin
    {
        private static CanvasGroup MainPanelInteraction;
        private static RectTransform BlacklistWindow;
        private static RectTransform BlacklistContent;
        private static bool BlacklistRefreshPending;
        private static CanvasGroup BlacklistWindowVisibility;
        private static float NextPlayerCardRefresh;
        private static string PlayerCardSignature = string.Empty;
        private sealed class AvatarBinding { public ulong Id; public RawImage Image; public TMP_Text Placeholder; public Texture2D RoundedTexture; }
        private static readonly List<AvatarBinding> PlayerAvatars = new();
        private static readonly List<AvatarBinding> BlacklistAvatars = new();
        private static readonly Color FriendBlue = new Color(0.13f, 0.39f, 0.73f, 1f);
        private static readonly Color EliminateYellow = new Color(0.94f, 0.72f, 0.18f, 1f);
        private static readonly Color KickRed = new Color(0.74f, 0.19f, 0.19f, 1f);
        private static readonly Color BanBlack = new Color(0.12f, 0.14f, 0.17f, 1f);
        private static Sprite AvatarMaskSprite;


        private static UIButton CardAction(Transform parent, string name, string label, Color background, Color foreground, Action action)
        {
            var button = NativeButton(parent, name, label, ButtonStyle.Secondary, 92f, 44f, action);
            SetPlayerActionColors(button, background, foreground);
            return button;
        }

        private static void SetPlayerActionColors(UIButton button, Color background, Color foreground)
        {
            button.bgNormal = background;
            button.bgHover = Color.Lerp(background, Color.white, 0.12f);
            button.bgPress = Color.Lerp(background, Color.black, 0.15f);
            button.bgSelected = button.bgHover;
            button.fgNormal = foreground;
            button.fgHover = foreground;
            button.fgSelected = foreground;
            button.Snap();
        }

        private static Texture2D MakeRoundedAvatarTexture(Texture source)
        {
            if (source == null) return null;
            var width = source.width;
            var height = source.height;
            if (width < 1 || height < 1) return null;
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readback.name = "HostControlAvatarReadback";
            readback.hideFlags = HideFlags.HideAndDontSave;
            var renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var previousActive = RenderTexture.active;
            Color32[] pixels;
            try
            {
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readback.Apply(false, false);
                pixels = readback.GetPixels32();
            }
            catch
            {
                UnityEngine.Object.Destroy(readback);
                return null;
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
            UnityEngine.Object.Destroy(readback);
            const float radiusFraction = 0.125f;
            var radius = Mathf.Min(width, height) * radiusFraction;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var dx = x < radius ? radius - 0.5f - x : x >= width - radius ? x - (width - radius - 0.5f) : 0f;
                var dy = y < radius ? radius - 0.5f - y : y >= height - radius ? y - (height - radius - 0.5f) : 0f;
                var distance = Mathf.Sqrt(dx * dx + dy * dy);
                var coverage = distance <= radius - 0.5f ? 1f : distance >= radius + 0.5f ? 0f : radius + 0.5f - distance;
                var index = y * width + x;
                var pixel = pixels[index];
                pixel.a = (byte)Mathf.RoundToInt(pixel.a * coverage);
                pixels[index] = pixel;
            }
            var rounded = new Texture2D(width, height, TextureFormat.RGBA32, false);
            rounded.name = "HostControlRoundedAvatar";
            rounded.hideFlags = HideFlags.HideAndDontSave;
            rounded.SetPixels32(pixels);
            rounded.Apply(false, true);
            return rounded;
        }

        private static Sprite GetAvatarMaskSprite()
        {
            if (AvatarMaskSprite != null) return AvatarMaskSprite;
            const int textureSize = 64;
            const float radius = 8f;
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.name = "HostControlAvatarRoundedMask";
            texture.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color32[textureSize * textureSize];
            var cornerCenter = textureSize - radius - 0.5f;
            for (var y = 0; y < textureSize; y++)
            for (var x = 0; x < textureSize; x++)
            {
                var dx = x < radius ? radius - 0.5f - x : x >= textureSize - radius ? x - cornerCenter : 0f;
                var dy = y < radius ? radius - 0.5f - y : y >= textureSize - radius ? y - cornerCenter : 0f;
                var distance = Mathf.Sqrt(dx * dx + dy * dy);
                var alpha = distance <= radius - 0.5f ? 1f : distance >= radius + 0.5f ? 0f : radius + 0.5f - distance;
                pixels[y * textureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            AvatarMaskSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize, 0, SpriteMeshType.Tight);
            AvatarMaskSprite.name = "HostControlAvatarRoundedMask";
            AvatarMaskSprite.hideFlags = HideFlags.HideAndDontSave;
            return AvatarMaskSprite;
        }

        private static void AddSteamAvatar(Transform parent, ulong id, string displayName, List<AvatarBinding> bindings, float size = 56f)
        {
            var root = UIF.Panel(parent, "SteamAvatar", UITheme.Paper, UITheme.RadiusSmall, false, false).Root;
            UIF.LE(root.gameObject, size, size, 0f, 0f, size, size);
            var maskGraphic = root.GetComponent<Image>();
            if (maskGraphic != null)
            {
                maskGraphic.sprite = GetAvatarMaskSprite();
                maskGraphic.type = Image.Type.Simple;
                maskGraphic.color = Color.white;
                maskGraphic.raycastTarget = false;
            }
            var mask = root.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var initial = string.IsNullOrWhiteSpace(displayName) ? "?" : System.Globalization.StringInfo.GetNextTextElement(displayName);
            var placeholder = NativeFlowText(root, "Initial", initial, UITheme.SizeBody, UITheme.InkSoft, TextAlignmentOptions.Center, false).GetComponent<TMP_Text>();
            UIF.Stretch(placeholder.GetComponent<RectTransform>());
            placeholder.raycastTarget = false;
            var photo = UIF.Rect("Photo", root);
            UIF.Stretch(photo);
            var image = photo.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
            bindings.Add(new AvatarBinding { Id = id, Image = image, Placeholder = placeholder });
        }

        private static RectTransform PersonCard(Transform parent, string name, out RectTransform texts, out RectTransform actions, bool blacklist = false)
        {
            var card = UIF.Panel(parent, name, UITheme.PaperDk, UITheme.RadiusSmall, false, false).Root;
            var cardHeight = blacklist ? 76f : 68f;
            UIF.LE(card.gameObject, -1f, cardHeight, 1f, 0f, 0f, cardHeight);
            UIF.HLayout(card.gameObject, 8f, UIF.Pad(8, 8, 4, 4), TextAnchor.MiddleLeft, true, true, false, false);
            texts = null;
            actions = null;
            return card;
        }

        private static void PersonColumns(RectTransform card, out RectTransform texts, out RectTransform actions, bool blacklist = false)
        {
            texts = UIF.Rect("Texts", card);
            UIF.LE(texts.gameObject, 0f, -1f, 1f, 0f, blacklist ? 180f : 140f, 0f);
            UIF.VLayout(texts.gameObject, 3f, UIF.Pad(0), TextAnchor.MiddleLeft, true, true, true, false);
            actions = UIF.Rect("Actions", card);
            UIF.LE(actions.gameObject, 0f, -1f, 0f, 0f, 0f, 0f);
            UIF.HLayout(actions.gameObject, blacklist ? 8f : 6f, UIF.Pad(0), TextAnchor.MiddleRight, true, true, false, false);
        }

        private static bool IsSteamFriend(ulong steamId)
        {
            if (steamId == 0) return false;
            try
            {
                return SteamFriends.GetFriendRelationship(new CSteamID(steamId)) == EFriendRelationship.k_EFriendRelationshipFriend;
            }
            catch
            {
                return false;
            }
        }

        private static void PersonText(Transform parent, string name, string value, float size, Color color)
        {
            var text = NativeFlowText(parent, name, value, size, color, TextAlignmentOptions.Left, true).GetComponent<TMP_Text>();
            text.richText = false;
            UIF.LE(text.gameObject, -1f, size + 8f, 1f, 0f, 1f, 0f);
        }

        private static string CurrentPlayerSignature()
        {
            return string.Join("|", Online.OrderBy(x => x.Key.connectionId).Select(x =>
            {
                var player = GetLivePlayer(x.Value.SteamId);
                return x.Value.SteamId + ":" + x.Value.Name + ":" + (player == null ? "pending" : IsPlayerEliminated(player).ToString());
            }));
        }

        private static void BuildPlayerCards()
        {
            PlayerAvatars.Clear();
            var listLayout = GuiContent.GetComponent<VerticalLayoutGroup>();
            if (listLayout != null) listLayout.spacing = 6f;
            PlayerCardSignature = CurrentPlayerSignature();
            NativeSettingsSection(GuiContent, Loc.T("Online remote players ({0})", Online.Count));
            if (Online.Count == 0)
            {
                var emptyState = UIF.Rect("Players.EmptyState", GuiContent);
                UIF.LE(emptyState.gameObject, -1f, 440f, 1f, 0f, 1f, 440f);
                UIF.VLayout(emptyState.gameObject, 18f, UIF.Pad(0), TextAnchor.MiddleCenter, true, true, false, false);
                var glyph = UIF.Glyph(emptyState, "EmptyPlayersGlyph", GlyphId.Users, UITheme.InkSoft, 112f);
                UIF.LE(glyph.gameObject, 112f, 112f, 0f, 0f, 112f, 112f);
                var empty = NativeFlowText(emptyState, "Players.Empty", Loc.T("No remote players online."), UITheme.SizeBody, UITheme.InkSoft, TextAlignmentOptions.Center, true);
                UIF.LE(empty.gameObject, -1f, 42f, 1f, 0f, 1f, 42f);
                return;
            }
            foreach (var pair in Online.OrderBy(x => x.Key.connectionId).ToArray())
            {
                var id = pair.Value.SteamId;
                var displayName = string.IsNullOrWhiteSpace(pair.Value.Name) ? Loc.T("Unnamed player") : pair.Value.Name;
                var player = GetLivePlayer(id);
                var dead = player != null && IsPlayerEliminated(player);
                var card = PersonCard(GuiContent, "PlayerCard." + id, out _, out _);
                AddSteamAvatar(card, id, displayName, PlayerAvatars, 48f);
                PersonColumns(card, out var texts, out var actions);
                PersonText(texts, "Name", displayName, UITheme.SizeBody * 0.9f, UITheme.Ink);
                PersonText(texts, "State", player == null ? Loc.T("Player is not initialized") : Loc.T(dead ? "Eliminated" : "Alive"), UITheme.SizeSmall * 0.9f, UITheme.InkSoft);
                var hostTarget = IsLocalHostSteamId(id);
                if (dead && !hostTarget)
                    CardAction(actions, "Respawn", Loc.T("Respawn"), UITheme.TealDk, Color.white, () => RunPanelAction(() => RespawnManagedPlayer(id)));
                CardAction(actions, "SteamProfile", Loc.T("Steam profile"), FriendBlue, Color.white, () => RunPanelAction(() => OpenSteamProfile(id)));
                if (!hostTarget && !IsSteamFriend(id))
                    CardAction(actions, "AddFriend", Loc.T("Add friend"), FriendBlue, Color.white, () => RunPanelAction(() => AddSteamFriend(id)));
                var eliminate = CardAction(actions, "Eliminate", Loc.T("Eliminate"), EliminateYellow, UITheme.Ink, () => RunPanelAction(() => EliminateManagedPlayer(id)));
                eliminate.SetInteractable(player != null && !dead && !hostTarget);
                var kick = CardAction(actions, "Kick", Loc.T("Kick"), KickRed, Color.white, () => RunPanelAction(() => Execute("kick " + id)));
                kick.SetInteractable(!hostTarget);
                var ban = CardAction(actions, "Ban", Loc.T("Ban"), BanBlack, Color.white, () => RunPanelAction(() => Execute("ban " + id)));
                ban.SetInteractable(!hostTarget);
            }
        }

        private static void OpenBlacklistWindow()
        {
            if (!GuiVisible || GuiRoot == null || BlacklistWindow != null) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (GuiContent != null) SmoothListScroll.Cancel(GuiContent.GetComponentInParent<ScrollRect>());
            if (MainPanelInteraction != null) { MainPanelInteraction.interactable = false; MainPanelInteraction.blocksRaycasts = false; }
            BlacklistWindow = UIF.Rect("BlacklistWindow", GuiRoot);
            UIF.Stretch(BlacklistWindow);
            BlacklistWindowVisibility = BlacklistWindow.gameObject.AddComponent<CanvasGroup>();
            BlacklistWindowVisibility.alpha = 0f;
            BlacklistWindowVisibility.interactable = false;
            BlacklistWindowVisibility.blocksRaycasts = false;
            UIF.Dimmer(BlacklistWindow, 0.55f);
            var card = UIF.Panel(BlacklistWindow, "Card", UITheme.Paper, UITheme.RadiusLarge, true, true).Root;
            UIF.At(card, UIF.Center, Vector2.zero, new Vector2(1120f, 760f));
            NativeText(card, "Title", Loc.T("Blacklist"), UITheme.SizeHeader, UITheme.Navy, TextAlignmentOptions.Left, 28f, 20f, 1064f, 48f, false);
            var scroll = UIF.Scroll(card, "BlacklistScroll", out BlacklistContent, pad: UIF.Pad(0, 0, 8, 12));
            UIF.At(scroll.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(28f, -84f), new Vector2(1064f, 564f));
            var contentLayout = BlacklistContent.GetComponent<VerticalLayoutGroup>();
            if (contentLayout != null) contentLayout.spacing = 6f;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            SmoothListScroll.Register(scroll);
            NativeButton(card, "CloseBlacklist", Loc.T("Close panel"), ButtonStyle.Primary, 832f, 680f, 260f, 52f, CloseBlacklistWindow);
            BuildBlacklistCards();
            BlacklistWindow.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(BlacklistWindow);
            LayoutRebuilder.ForceRebuildLayoutImmediate(BlacklistContent);
            FitPersonCards(BlacklistContent);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(BlacklistWindow);
            BlacklistWindowVisibility.alpha = 1f;
            BlacklistWindowVisibility.interactable = true;
            BlacklistWindowVisibility.blocksRaycasts = true;
        }

        private static void CloseBlacklistWindow()
        {
            if (BlacklistWindow == null) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            BlacklistWindow.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(BlacklistWindow.gameObject);
            BlacklistWindow = null;
            BlacklistContent = null;
            BlacklistWindowVisibility = null;
            if (MainPanelInteraction != null) { MainPanelInteraction.interactable = true; MainPanelInteraction.blocksRaycasts = true; }
            BlacklistAvatars.Clear();
            BlacklistRefreshPending = false;
            if (BlacklistFooterButton != null) UIScreen.Select(BlacklistFooterButton.gameObject);
        }

        private static void BuildBlacklistCards()
        {
            if (BlacklistContent == null) return;
            BlacklistRefreshPending = false;
            BlacklistAvatars.Clear();
            for (var i = BlacklistContent.childCount - 1; i >= 0; i--)
            {
                var child = BlacklistContent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            NativeSettingsSection(BlacklistContent, Loc.T("Blacklist ({0})", Blacklist.Count));
            if (Blacklist.Count == 0)
            {
                PersonText(BlacklistContent, "Empty", Loc.T("No banned players."), UITheme.SizeBody, UITheme.InkSoft);
                return;
            }
            foreach (var id in Blacklist.OrderBy(x => x).ToArray())
            {
                BanRecords.TryGetValue(id, out var record);
                var displayName = string.IsNullOrWhiteSpace(record?.PlayerName) ? Loc.T("Player name not recorded") : record.PlayerName;
                var card = PersonCard(BlacklistContent, "BlacklistCard." + id, out _, out _, blacklist: true);
                AddSteamAvatar(card, id, displayName, BlacklistAvatars, 64f);
                PersonColumns(card, out var texts, out var actions, blacklist: true);
                PersonText(texts, "Name", displayName, UITheme.SizeBody, UITheme.Ink);
                var time = record?.BannedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? Loc.T("Not recorded (legacy list)");
                PersonText(texts, "Details", Loc.T("SteamID: {0}", id) + " · " + Loc.T("Banned at: {0}", time), UITheme.SizeSmall * 0.85f, UITheme.InkSoft);
                CardAction(actions, "SteamProfile", Loc.T("Steam profile"), FriendBlue, Color.white, () => RunPanelAction(() => OpenSteamProfile(id)));
                CardAction(actions, "Unban", Loc.T("Remove from blacklist"), KickRed, Color.white, () =>
                {
                    RunPanelAction(() => Execute("unban " + id));
                    BlacklistRefreshPending = true;
                });
            }
        }

        private static void UpdatePlayerCardUi()
        {
            if (Time.unscaledTime < NextPlayerCardRefresh) return;
            NextPlayerCardRefresh = Time.unscaledTime + 0.5f;
            if (BlacklistRefreshPending && BlacklistWindow != null)
            {
                if (BlacklistWindowVisibility != null)
                {
                    BlacklistWindowVisibility.alpha = 0f;
                    BlacklistWindowVisibility.interactable = false;
                    BlacklistWindowVisibility.blocksRaycasts = false;
                }
                BuildBlacklistCards();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(BlacklistContent);
                FitPersonCards(BlacklistContent);
                Canvas.ForceUpdateCanvases();
                if (BlacklistWindowVisibility != null)
                {
                    BlacklistWindowVisibility.alpha = 1f;
                    BlacklistWindowVisibility.interactable = true;
                    BlacklistWindowVisibility.blocksRaycasts = true;
                }
            }
            if (GuiTab == 1 && BlacklistWindow == null && CurrentPlayerSignature() != PlayerCardSignature)
            {
                var scroll = GuiContent.GetComponentInParent<ScrollRect>();
                var position = scroll != null ? scroll.verticalNormalizedPosition : 1f;
                RebuildContent();
                Canvas.ForceUpdateCanvases();
                if (scroll != null) scroll.verticalNormalizedPosition = position;
            }
            foreach (var binding in PlayerAvatars.Concat(BlacklistAvatars))
            {
                if (binding.Image == null || binding.Id == 0) continue;
                try
                {
                    if (binding.RoundedTexture != null)
                    {
                        binding.Image.texture = binding.RoundedTexture;
                        binding.Image.enabled = true;
                        continue;
                    }
                    var texture = SteamAvatars.Get(binding.Id);
                    if (texture == null) continue;
                    binding.RoundedTexture = MakeRoundedAvatarTexture(texture);
                    if (binding.RoundedTexture == null)
                    {
                        LogEvent("avatar-rounding-skipped", "steamId=" + binding.Id + " readable=" + (texture as Texture2D)?.isReadable);
                        continue;
                    }
                    LogEvent("avatar-rounded", "steamId=" + binding.Id + " width=" + binding.RoundedTexture.width + " height=" + binding.RoundedTexture.height);
                    binding.Image.texture = binding.RoundedTexture;
                    binding.Image.enabled = true;
                    if (binding.Placeholder != null) binding.Placeholder.enabled = false;
                }
                catch { /* Steam may be unavailable; retain the placeholder. */ }
            }
            FitPersonCards(GuiContent);
            if (BlacklistContent != null) FitPersonCards(BlacklistContent);
        }

        private static void FitPersonCards(RectTransform content)
        {
            if (content == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            foreach (var group in content.GetComponentsInChildren<HorizontalLayoutGroup>())
            {
                if (!group.name.StartsWith("PlayerCard.") && !group.name.StartsWith("BlacklistCard.")) continue;
                var texts = group.transform.Find("Texts");
                var actions = group.transform.Find("Actions");
                var element = group.GetComponent<LayoutElement>();
                if (texts == null || actions == null || element == null) continue;
                var actionLayout = actions.GetComponent<HorizontalLayoutGroup>();
                var naturalWidth = 0f;
                var cardWidth = group.GetComponent<RectTransform>().rect.width;
                var blacklist = group.name.StartsWith("BlacklistCard.");
                var avatarSize = blacklist ? 64f : 48f;
                var available = Mathf.Max(1f, cardWidth - group.padding.left - group.padding.right - avatarSize - (blacklist ? 400f : 140f) - group.spacing * 2f);
                var labels = actions.GetComponentsInChildren<TMP_Text>();
                var textWidthTotal = 0f;
                const float horizontalPadding = 28f;
                foreach (var label in labels)
                {
                    label.enableAutoSizing = false;
                    label.enableWordWrapping = false;
                    label.overflowMode = TextOverflowModes.Overflow;
                    label.fontSize = UITheme.SizeSmall;
                    textWidthTotal += label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity).x;
                }
                var gaps = Mathf.Max(0, actions.childCount - 1) * actionLayout.spacing;
                var textBudget = Mathf.Max(1f, available - gaps - actions.childCount * horizontalPadding);
                var fontScale = textWidthTotal > textBudget ? Mathf.Max(0.8f, textBudget / textWidthTotal) : 1f;
                // Prefer full labels over a fixed information-column reservation in long translations.
                // The remaining name/details column can wrap; action labels must always stay on one line.
                for (var i = 0; i < actions.childCount; i++)
                {
                    var buttonRect = actions.GetChild(i).GetComponent<RectTransform>();
                    var label = buttonRect.GetComponentInChildren<TMP_Text>();
                    var le = buttonRect.GetComponent<LayoutElement>();
                    if (label == null || le == null) continue;
                    label.fontSize = UITheme.SizeSmall * fontScale;
                    label.enableWordWrapping = false;
                    var preferred = label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity);
                    var width = Mathf.Ceil(preferred.x + horizontalPadding + 2f);
                    var requiredHeight = Mathf.Max(38f, Mathf.Ceil(preferred.y + 12f));
                    var labelRect = label.GetComponent<RectTransform>();
                    UIF.Stretch(labelRect);
                    labelRect.offsetMin = new Vector2(horizontalPadding * 0.5f, 4f);
                    labelRect.offsetMax = new Vector2(-horizontalPadding * 0.5f, -4f);
                    label.alignment = TextAlignmentOptions.Center;
                    var button = buttonRect.GetComponent<UIButton>();
                    if (button != null) { button.hoverScale = 1f; button.Snap(); }
                    le.minWidth = width; le.preferredWidth = width;
                    le.minHeight = requiredHeight; le.preferredHeight = requiredHeight;
                    buttonRect.sizeDelta = new Vector2(width, requiredHeight);
                    naturalWidth += width;
                }
                naturalWidth += Mathf.Max(0, actions.childCount - 1) * actionLayout.spacing;
                var actionsElement = actions.GetComponent<LayoutElement>();
                actionsElement.preferredWidth = naturalWidth;
                actionsElement.minWidth = naturalWidth;
                var height = avatarSize;
                var textHeight = 0f;
                foreach (var text in texts.GetComponentsInChildren<TMP_Text>())
                {
                    var width = text.GetComponent<RectTransform>().rect.width;
                    if (width < 1f) continue;
                    var required = MeasureTextHeight(text, width);
                    var le = text.GetComponent<LayoutElement>();
                    le.minHeight = required; le.preferredHeight = required;
                    textHeight += required;
                }
                textHeight += Mathf.Max(0, texts.childCount - 1) * 3f;
                height = Mathf.Max(height, textHeight);
                for (var i = 0; i < actions.childCount; i++)
                    height = Mathf.Max(height, LayoutUtility.GetPreferredHeight(actions.GetChild(i).GetComponent<RectTransform>()));
                height += group.padding.top + group.padding.bottom;
                element.minHeight = height; element.preferredHeight = height;
            }
            LayoutRebuilder.MarkLayoutForRebuild(content);
        }
    }
}
