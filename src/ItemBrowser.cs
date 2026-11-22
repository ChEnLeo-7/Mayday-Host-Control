using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMayday;
using Il2CppMayday.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MaydayHostControl
{
    public sealed partial class Plugin
    {
        private const int ItemsTabIndex = 3;
        private const int SettingsTabIndex = 4;

        private sealed class ItemEntry
        {
            public string Id;
            public string NameKey;
            public string Name;
            public Il2CppMayday.ItemCategory Category;
            public bool Implemented;
            public bool Special;
        }

        private sealed class ItemTileView
        {
            public Image Bg;
            public TMP_Text Check;
            public TMP_Text Favorite;
            public RectTransform Root;
            public TMP_Text Name;
            public Outline StateOutline;
            public GameObject Hit;
        }

        private static readonly List<ItemEntry> ItemEntries = new();
        private static readonly List<GrantTarget> ItemTargets = new();
        private static readonly Dictionary<string, ItemTileView> ItemTileViews = new();
        private static string ItemTileHoveredId;
        private static EventSystem ItemTileEventSystem;
        private static PointerEventData ItemTilePointer;
        private static Il2CppSystem.Collections.Generic.List<RaycastResult> ItemTileRaycastHits;
        private static readonly List<UIButton> ItemHoverButtons = new();
        private static UIButton ItemPointerButton;
        private static UISelector ItemPointerSelector;
        private static readonly List<UIButton> ItemChipButtons = new();
        private static readonly List<string> ItemChipValues = new();
        private static readonly HashSet<string> ItemFavorites = new(StringComparer.OrdinalIgnoreCase);
        private static bool ItemFavoritesPage;
        private static float ItemSearchFocusClearAt;
        private static UIButton ItemFavoritesHeaderButton;
        private static UIButton ItemDockFavoriteButton;
        private static Image ItemDockFavoriteEmptyIcon;
        private static Image ItemDockFavoriteFullIcon;
        private static UIButton ItemDeliverySettingsButton;
        private static RectTransform ItemDestHighlight;

        private static bool ItemCatalogBuilt;
        private static string ItemSearchText = string.Empty;
        private static string ItemCategoryFilter = string.Empty;
        private static bool ItemSearchDirty;
        private static float ItemSearchDue;
        private static int ItemQuantity = 1;
        private static int ItemDestination;
        private static string ItemSelectedId = string.Empty;
        private static string ItemCustomId;
        private static ItemEntry ItemSelectedEntry;
        private static bool ItemSpecialArmed;
        private static float ItemSpecialArmedUntil;
        private static string ItemStatus = string.Empty;
        private static bool ItemStatusError;
        private static float ItemStatusUntil;

        private static RectTransform ItemGridRoot;
        private const int ItemGridColumns = 5;
        private const int ItemGridVisibleRows = 3;
        private const float ItemGridPadding = 7f;
        private const float ItemGridBottomSafetyInset = 4f;
        private const float ItemTileHeight = 112f;
        private const float ItemColumnGap = 8f;
        private const float ItemRowGap = 5f;
        private const float ItemHoverDarken = 0.07f;
        // Category chips keep the same ink tint as the rest of the page but a little deeper.
        private const float ItemChipHoverDarken = 0.12f;
        private static readonly List<RectTransform> ItemGridRows = new();
        private static ScrollRect ItemGridScroll;
        private static RectTransform ItemSearchRow;
        private static RectTransform ItemIdRow;
        private static RectTransform ItemDockRoot;
        private static RectTransform ItemDockDivider;
        private static RectTransform ItemDockIconRoot;
        private static Image ItemDockIcon;
        private static TMP_Text ItemDockIconFallback;
        private static TMP_Text ItemDockName;
        private static TMP_Text ItemDockMeta;
        private static TMP_Text ItemQuantityText;
        private static TMP_InputField ItemQuantityInput;
        private static TMP_Text ItemTargetCaptionLabel;
        private static UIButton ItemTargetCaptionButton;
        private static GameObject ItemPopupRoot;
        private static TMP_InputField ItemIdPopupInput;
        private static TMP_Text ItemDeliverySummaryLabel;
        private static TMP_InputField ItemDeliveryQtyInput;
        private static Image DeliveryQuantityMinusHighlight;
        private static Image DeliveryQuantityInputHighlight;
        private static Image DeliveryQuantityPlusHighlight;
        private static Image DeliveryDestinationPrevHighlight;
        private static Image DeliveryDestinationNextHighlight;
        private static TMP_Text ItemDeliveryCopyHint;
        private static float ItemDeliveryCopyUntil;
        private static UIButton ItemDeliveryTargetButton;
        private static TMP_Text ItemDeliveryTargetLabel;
        private static UISelector ItemDeliveryDestSelector;
        private static Transform ItemDeliveryTargetControl;
        private static Transform ItemDeliveryDestinationControl;
        private static TMP_InputField ItemSearchInput;
        private static TMP_InputField ItemIdInput;
        private static UIButton ItemSpawnButton;
        private static UIButton ItemBackpackButton;
        private static UIButton ItemGroundButton;
        private static UIButton ItemQuantityMinusButton;
        private static UIButton ItemQuantityPlusButton;

        private static readonly string[] ItemChipDefinitions =
        {
            string.Empty, "Cabin", "Galley", "Cleaning", "Maintenance", "Luggage", "Crate", "Misc"
        };

        private static void NoRaycast(UIText text)
        {
            var tmp = text != null ? text.GetComponent<TMP_Text>() : null;
            if (tmp != null) tmp.raycastTarget = false;
        }

        private static UIButton ItemTextButton(Transform parent, string name, string label, ButtonStyle style,
            float x, float y, float width, float height, Action action, float fontSize)
        {
            var button = UIF.Button(parent, name, label, NativeAction(action), style, width, height,
                new Il2CppSystem.Nullable<GlyphId>(), -1f, -1f);
            UIF.At(button.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(x, -y), new Vector2(width, height));
            UIF.LE(button.gameObject, width, height, 0f, 0f, width, height);
            button.hoverScale = 1f;
            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.enableAutoSizing = false;
                text.fontSize = fontSize;
                text.fontSizeMax = fontSize;
                text.fontSizeMin = fontSize;
                text.enableWordWrapping = true;
                text.overflowMode = TextOverflowModes.Overflow;
                text.margin = new Vector4(8f, 2f, 8f, 2f);
            }
            foreach (var shadow in button.GetComponentsInChildren<UnityEngine.UI.Shadow>(true)) shadow.enabled = false;
            TrackItemHoverButton(button);
            return button;
        }

        private static void TrackItemHoverButton(UIButton button)
        {
            if (button == null || ItemHoverButtons.Contains(button)) return;
            if (button.Ring != null) button.Ring.raycastTarget = false;
            ItemHoverButtons.Add(button);
        }

        private static float ItemButtonWidth(UIButton button, float minimum = 48f, float padding = 32f)
        {
            var text = button.GetComponentInChildren<TMP_Text>();
            return text == null ? minimum : Mathf.Max(minimum,
                Mathf.Ceil(text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x + padding));
        }

        private static void PlaceItemButton(UIButton button, float x, float y, float width, float height)
        {
            UIF.At(button.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(x, -y), new Vector2(width, height));
            var layout = button.GetComponent<LayoutElement>();
            if (layout == null) return;
            layout.minWidth = layout.preferredWidth = width;
            layout.minHeight = layout.preferredHeight = height;
        }

        private static void BuildItems()
        {
            var itemLayout = GuiContent.GetComponent<VerticalLayoutGroup>();
            if (itemLayout != null) itemLayout.spacing = 0f;
            CloseItemPopup();
            DestroyItemDock();
            EnsureItemEntries();
            ItemGridRows.Clear();
            RefreshItemNames(Loc.LanguageCodes[Loc.CurrentIndex]);
            BuildItemTargets();
            EnsureItemFavoritesHeaderButton();
            BuildItemSpacer(16f);

            if (ItemEntries.Count == 0)
            {
                NativeDisplayRow(GuiContent, Loc.T("Items"), Loc.T("No eligible items available"), true);
                return;
            }

            if (ItemFavoritesPage)
            {
                BuildItemFavoritesHeader();
                BuildItemSpacer(12f);
                if (!HasCatalogFavorites())
                {
                    BuildItemFavoritesEmpty();
                    return;
                }
            }
            else
            {
                BuildItemSearchRow();
                BuildItemSpacer(12f);
                BuildItemChipsRow();
                BuildItemSpacer(12f);
                ItemSearchFocusClearAt = Time.unscaledTime + 0.05f;
            }

            BuildItemGrid();
            BuildItemDock();
            UpdateItemChipStyles();
            RefreshItemGrid();
            UpdateItemDock();
            if (ItemSearchInput != null) ItemSearchInput.DeactivateInputField();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private static void EnsureItemFavoritesHeaderButton()
        {
            if (ItemFavoritesHeaderButton != null)
            {
                ItemFavoritesHeaderButton.gameObject.SetActive(true);
                SetFavoritePageButtonStyle();
                return;
            }
            ItemFavoritesHeaderButton = ItemTextButton(GuiCardRoot, "ItemFavoritesHeader",
                "☆  " + Loc.T("Favorites"), ButtonStyle.Secondary, 988f, 22f, 160f, 40f,
                ShowItemFavoritesPage, 16f);
            var width = ItemButtonWidth(ItemFavoritesHeaderButton, 96f);
            PlaceItemButton(ItemFavoritesHeaderButton, 1148f - width, 22f, width, 40f);
            SetFavoritePageButtonStyle();
        }

        private static void SetFavoritePageButtonStyle()
        {
            if (ItemFavoritesHeaderButton == null) return;
            var selected = ItemFavoritesPage;
            // Selected state reads as filled star, matching the dock's favourite glyph.
            var label = (selected ? "★  " : "☆  ") + Loc.T("Favorites");
            var text = ItemFavoritesHeaderButton.GetComponentInChildren<TMP_Text>();
            if (text != null && !string.Equals(text.text, label, StringComparison.Ordinal))
            {
                text.text = label;
                var width = ItemButtonWidth(ItemFavoritesHeaderButton, 96f);
                PlaceItemButton(ItemFavoritesHeaderButton, 1148f - width, 22f, width, 40f);
            }
            ItemFavoritesHeaderButton.SetSelected(selected);
            SetPlayerActionColors(ItemFavoritesHeaderButton,
                selected ? UITheme.TealDk : UITheme.PaperDk,
                selected ? Color.white : UITheme.Ink);
        }

        private static void BuildItemSpacer(float height)
        {
            var spacer = UIF.Rect("ItemSpacer", GuiContent);
            UIF.LE(spacer.gameObject, -1f, height, 1f, 0f, -1f, height);
        }

        private static int CatalogFavoriteCount()
        {
            var count = 0;
            for (var i = 0; i < ItemEntries.Count; i++)
                if (IsFavorite(ItemEntries[i].Id)) count++;
            return count;
        }

        private static bool HasCatalogFavorites()
        {
            return CatalogFavoriteCount() > 0;
        }

        private static void ShowItemFavoritesPage()
        {
            ItemFavoritesPage = true;
            SetFavoritePageButtonStyle();
            ItemCategoryFilter = string.Empty;
            ItemSearchText = string.Empty;
            ItemSearchDirty = false;
            ItemSelectedId = string.Empty;
            ItemSelectedEntry = null;
            ItemCustomId = null;
            ItemSpecialArmed = false;
            RebuildContent();
        }

        private static void ShowItemPage()
        {
            ItemFavoritesPage = false;
            SetFavoritePageButtonStyle();
            RebuildContent();
        }

        private static void BuildItemFavoritesHeader()
        {
            var row = UIF.Rect("ItemFavoritesHeader", GuiContent);
            UIF.LE(row.gameObject, -1f, 34f, 1f, 0f, -1f, 34f);

            var back = ItemTextButton(row, "ItemFavoritesBack", "< " + Loc.T("Items"), ButtonStyle.Ghost,
                0f, 1f, 120f, 30f, ShowItemPage, UITheme.SizeSmall);
            PlaceItemButton(back, 0f, 1f, ItemButtonWidth(back), 30f);
            SetPlayerActionColors(back, new Color(0f, 0f, 0f, 0f), UITheme.Ink);

            // The favorites title is already carried by the tab row and the header button, so the page
            // header only needs a back control on the left and the saved count on the right.
            var favoriteCount = CatalogFavoriteCount();
            if (favoriteCount > 0)
            {
                var count = NativeFlowText(row, "ItemFavoritesCount", "★  " + favoriteCount, UITheme.SizeTiny,
                    UITheme.Gold, TextAlignmentOptions.Right, false);
                NoRaycast(count);
                UIF.At(count.Rect, new Vector2(1f, 1f), new Vector2(-2f, -5f), new Vector2(90f, 24f));
            }

            var divider = UIF.Rect("ItemFavoritesHeaderLine", row);
            var dividerImage = divider.gameObject.AddComponent<Image>();
            dividerImage.color = UITheme.WithAlpha(UITheme.Ink, 0.10f);
            dividerImage.raycastTarget = false;
            divider.anchorMin = new Vector2(0f, 0f);
            divider.anchorMax = new Vector2(1f, 0f);
            divider.pivot = new Vector2(0.5f, 0f);
            divider.anchoredPosition = Vector2.zero;
            divider.sizeDelta = new Vector2(0f, 1f);
        }

        private static void BuildItemFavoritesEmpty()
        {
            var empty = UIF.Rect("ItemFavoritesEmpty", GuiContent);
            // Fill the remaining content height so the hint sits in the middle of the page instead of
            // hugging the header when nothing is saved yet.
            UIF.LE(empty.gameObject, -1f, 360f, 1f, 1f, -1f, 360f);
            UIF.VLayout(empty.gameObject, 12f, UIF.Pad(0), TextAnchor.MiddleCenter, true, true, false, false);

            var glyph = UIF.Glyph(empty, "ItemFavoritesEmptyGlyph", GlyphId.StarEmpty, UITheme.InkSoft, 96f);
            UIF.LE(glyph.gameObject, 96f, 96f, 0f, 0f, 96f, 96f);
            var title = NativeFlowText(empty, "ItemFavoritesEmptyTitle", Loc.T("No favorites yet"), UITheme.SizeBody,
                UITheme.Ink, TextAlignmentOptions.Center, false);
            NoRaycast(title);
            UIF.LE(title.gameObject, -1f, 30f, 1f, 0f, 1f, 30f);
            var hint = NativeFlowText(empty, "ItemFavoritesEmptyHint",
                Loc.T("Tap the star on an item to save it here"), UITheme.SizeTiny, UITheme.InkSoft,
                TextAlignmentOptions.Center, false);
            NoRaycast(hint);
            UIF.LE(hint.gameObject, -1f, 20f, 1f, 0f, 1f, 20f);
        }

        private static void BuildItemTargets()
        {
            ItemTargets.Clear();
            Il2CppMayday.PlayerNet host = null;
            var local = Il2CppMirror.NetworkServer.localConnection;
            if (local != null && local.identity != null) host = local.identity.GetComponent<Il2CppMayday.PlayerNet>();
            if (host == null)
            {
                var all = Il2CppMayday.PlayerNet.All;
                if (all != null)
                {
                    for (var i = 0; i < all.Count; i++)
                    {
                        var candidate = all[i];
                        if (candidate != null && candidate.isLocalPlayer) { host = candidate; break; }
                    }
                }
            }
            if (host != null) ItemTargets.Add(new GrantTarget { Key = "host", Name = Loc.T("Host (local)"), Player = host });
            foreach (var pair in Online.OrderBy(x => x.Key.connectionId))
            {
                var player = GetLivePlayer(pair.Value.SteamId);
                if (player == null || player == host) continue;
                ItemTargets.Add(new GrantTarget
                {
                    Key = pair.Value.SteamId.ToString(),
                    Name = string.IsNullOrWhiteSpace(pair.Value.Name) ? Loc.T("Unnamed player") : pair.Value.Name,
                    Player = player
                });
            }
            if (ItemTargets.Count == 0 || !ItemTargets.Any(x => x.Key == SelectedGrantSteamId))
                SelectedGrantSteamId = ItemTargets.Count > 0 ? ItemTargets[0].Key : string.Empty;
        }

        private static void BuildItemTargetRow()
        {
            var control = NewNativeSettingRow(GuiContent, "ItemTarget", Loc.T("Target player"), string.Empty, 64f);
            var names = ItemTargets.Count > 0
                ? ItemTargets.Select(x => x.Name).ToArray()
                : new[] { "—" };
            var index = ItemTargets.FindIndex(x => x.Key == SelectedGrantSteamId);
            if (index < 0) index = 0;
            UIF.Selector(control, "ItemTargetSelector", new Il2CppStringArray(names), index,
                NativeIntAction(value =>
                {
                    if (value < 0 || value >= ItemTargets.Count) return;
                    SelectedGrantSteamId = ItemTargets[value].Key;
                }), 360f);

            var divider = UIF.Rect("ItemTargetDivider", control.parent);
            UIF.At(divider, UIF.TopLeft, new Vector2(0f, -63f), new Vector2(1116f, 1f));
            var dividerImage = divider.gameObject.AddComponent<Image>();
            dividerImage.color = UITheme.WithAlpha(UITheme.Teal, 0.55f);
            dividerImage.raycastTarget = false;
        }

        private static void BuildItemSearchRow()
        {
            ItemSearchRow = UIF.Rect("ItemSearchRow", GuiContent);
            UIF.LE(ItemSearchRow.gameObject, -1f, 56f, 1f, 0f, -1f, 56f);

            ItemSearchInput = UIF.Input(ItemSearchRow, "ItemSearchInput", ItemSearchText,
                Loc.T("Search by name or ID"), 48,
                NativeStringAction(value =>
                {
                    ItemSearchText = value ?? string.Empty;
                    ItemSearchDirty = true;
                    ItemSearchDue = Time.unscaledTime + 0.8f;
                }), 1000f, 44f);
            UIF.At(ItemSearchInput.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(0f, -6f), new Vector2(1000f, 44f));
            ItemSearchInput.pointSize = UITheme.SizeSmall;
            if (ItemSearchInput.textComponent != null) ItemSearchInput.textComponent.fontSize = UITheme.SizeSmall;
            var searchPlaceholder = ItemSearchInput.placeholder as TMP_Text;
            if (searchPlaceholder != null) searchPlaceholder.fontSize = UITheme.SizeSmall;

            var idToggle = ItemTextButton(ItemSearchRow, "ItemIdToggle", Loc.T("By ID"), ButtonStyle.Ghost,
                0f, 8f, 80f, 40f, ShowItemIdPopup, 16f);
            var idToggleWidth = ItemButtonWidth(idToggle, 64f);
            PlaceItemButton(idToggle, 1092f - idToggleWidth, 8f, idToggleWidth, 40f);
            ItemSearchInput.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1080f - idToggleWidth);
            SetPlayerActionColors(idToggle, new Color(0f, 0f, 0f, 0f), UITheme.InkSoft);
        }

        private static void BuildItemIdRow()
        {
            ItemIdRow = UIF.Rect("ItemIdRow", GuiContent);
            UIF.LE(ItemIdRow.gameObject, -1f, 56f, 1f, 0f, -1f, 56f);
            ItemIdRow.gameObject.SetActive(false);

            var line = UIF.Rect("ItemIdLine", ItemIdRow);
            var lineImage = line.gameObject.AddComponent<Image>();
            lineImage.color = UITheme.WithAlpha(UITheme.Ink, 0.10f);
            lineImage.raycastTarget = false;
            UIF.At(line, UIF.TopLeft, new Vector2(0f, 0f), new Vector2(1092f, 1f));

            var label = NativeFlowText(ItemIdRow, "ItemIdLabel", Loc.T("Item ID"), UITheme.SizeBody, UITheme.Ink,
                TextAlignmentOptions.Left, false);
            NoRaycast(label);
            UIF.At(label.Rect, UIF.TopLeft, new Vector2(0f, -16f), new Vector2(140f, 32f));

            ItemIdInput = UIF.Input(ItemIdRow, "ItemIdInput", string.Empty, Loc.T("Enter item ID"), 64,
                NativeStringAction(_ => { }), 380f, 38f);
            UIF.At(ItemIdInput.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(520f, -9f), new Vector2(380f, 38f));
            ItemIdInput.pointSize = UITheme.SizeSmall;
            if (ItemIdInput.textComponent != null) ItemIdInput.textComponent.fontSize = UITheme.SizeSmall;
            var idPlaceholder = ItemIdInput.placeholder as TMP_Text;
            if (idPlaceholder != null) idPlaceholder.fontSize = UITheme.SizeSmall;

            var use = ItemTextButton(ItemIdRow, "ItemIdUse", Loc.T("Use"), ButtonStyle.Secondary,
                0f, 9f, 80f, 38f, ApplyItemIdInput, UITheme.SizeSmall * 0.85f);
            var cancel = ItemTextButton(ItemIdRow, "ItemIdCancel", Loc.T("Cancel"), ButtonStyle.Ghost,
                0f, 9f, 80f, 38f, () => ShowItemIdRow(false), UITheme.SizeSmall * 0.85f);
            var cancelWidth = ItemButtonWidth(cancel, 64f);
            var useWidth = ItemButtonWidth(use, 64f);
            var useX = 1092f - cancelWidth - useWidth - 8f;
            PlaceItemButton(use, useX, 9f, useWidth, 38f);
            PlaceItemButton(cancel, 1092f - cancelWidth, 9f, cancelWidth, 38f);
            UIF.At(ItemIdInput.GetComponent<RectTransform>(), UIF.TopLeft,
                new Vector2(520f, -9f), new Vector2(useX - 532f, 38f));
        }

        private static void ShowItemIdRow(bool open)
        {
            if (ItemSearchRow == null || ItemIdRow == null) return;
            ItemSearchRow.gameObject.SetActive(!open);
            ItemIdRow.gameObject.SetActive(open);
            if (open)
            {
                if (ItemIdInput != null)
                {
                    ItemIdInput.text = string.Empty;
                    ItemIdInput.ActivateInputField();
                }
            }
            else if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private static void ApplyItemIdInput()
        {
            ApplyItemId(ItemIdInput != null ? ItemIdInput.text : string.Empty);
        }

        private static void ApplyItemId(string raw)
        {
            var id = (raw ?? string.Empty).Trim();
            if (id.Length == 0)
            {
                SetItemStatus(Loc.T("Please enter an item ID"), true);
                return;
            }
            if (id.Length > 128) id = id.Substring(0, 128);
            ItemEntry known = null;
            for (var i = 0; i < ItemEntries.Count; i++)
            {
                if (string.Equals(ItemEntries[i].Id, id, StringComparison.OrdinalIgnoreCase)) { known = ItemEntries[i]; break; }
            }
            if (known != null) SelectItem(known);
            else SelectCustomItem(id);
        }

        private static void BuildItemChipsRow()
        {
            var row = UIF.Rect("ItemChipsRow", GuiContent);
            UIF.LE(row.gameObject, -1f, 40f, 1f, 0f, -1f, 40f);
            ItemChipButtons.Clear();
            ItemChipValues.Clear();

            const float gap = 4f;
            const float chipHeight = 32f;
            var availableWidth = GuiContent != null ? GuiContent.rect.width : 1116f;
            if (availableWidth <= 0f) availableWidth = 1116f;
            var x = 0f;
            var y = 4f;
            for (var i = 0; i < ItemChipDefinitions.Length; i++)
            {
                var value = ItemChipDefinitions[i];
                var captured = value;
                var label = value.Length == 0 ? Loc.T("All") : ItemCategoryLabel(value);
                var button = ItemTextButton(row, "ItemChip." + (value.Length == 0 ? "All" : value), label,
                    ButtonStyle.Tab, 0f, 0f, 48f, chipHeight, () =>
                    {
                        ItemCategoryFilter = captured;
                        UpdateItemChipStyles();
                        RefreshItemGrid();
                    }, UITheme.SizeSmall);
                var width = ItemButtonWidth(button, 64f, 56f);
                RefreshItemChipPlate(button, false);
                if (x > 0f && x + width > availableWidth)
                {
                    x = 0f;
                    y += chipHeight + 4f;
                }
                PlaceItemButton(button, x, y, width, chipHeight);
                ItemChipButtons.Add(button);
                ItemChipValues.Add(value);
                x += width + gap;
            }
            var rowHeight = y + chipHeight + 4f;
            var layout = row.GetComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = rowHeight;

        }

        private static void UpdateItemChipStyles()
        {
            for (var i = 0; i < ItemChipButtons.Count && i < ItemChipValues.Count; i++)
            {
                var selected = string.Equals(ItemChipValues[i], ItemCategoryFilter, StringComparison.Ordinal);
                ItemChipButtons[i].SetSelected(selected);
            }
        }

        // The tab style re-applies its own 7% ink wash whenever the button is rebuilt or re-enabled,
        // so the chip plate is asserted from the pointer refresh loop instead of only at build time.
        private static void RefreshItemChipPlate(UIButton button, bool lit)
        {
            if (button == null) return;
            var clear = new Color(0f, 0f, 0f, 0f);
            button.bgNormal = clear;
            button.bgHover = Color.Lerp(clear, UITheme.Ink, ItemChipHoverDarken);
            button.bgPress = Color.Lerp(clear, UITheme.Ink, ItemChipHoverDarken * 1.3f);
            button.bgDisabled = clear;
            if (button.selected || button.Bg == null) return;
            var plate = lit ? button.bgHover : clear;
            if (button.Bg.color != plate) button.Bg.color = plate;
        }

        private static bool IsItemChipButton(UIButton button)
        {
            if (button == null) return false;
            for (var i = 0; i < ItemChipButtons.Count; i++)
                if (ItemChipButtons[i] == button) return true;
            return false;
        }

        private static void BuildItemGrid()
        {
            ItemGridScroll = UIF.Scroll(GuiContent, "ItemGridScroll", out ItemGridRoot, pad: UIF.Pad(0));
            var initialHeight = ItemGridPadding * 2f + ItemGridVisibleRows * ItemTileHeight + (ItemGridVisibleRows - 1) * ItemRowGap;
            UIF.LE(ItemGridScroll.gameObject, -1f, initialHeight, 1f, 0f, -1f, initialHeight);
            ItemGridScroll.horizontal = false;
            ItemGridScroll.vertical = true;
            ItemGridScroll.movementType = ScrollRect.MovementType.Clamped;
            ItemGridScroll.scrollSensitivity = 30f;
            // Keep the viewport width stable when filtering hides the native scrollbar.
            ItemGridScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            const float scrollbarWidth = 8f;
            const float scrollbarInset = 5f;
            const float scrollbarGap = 8f;
            var viewport = ItemGridScroll.viewport;
            UIF.Stretch(viewport, 0f, 0f, scrollbarWidth + scrollbarInset + scrollbarGap, 0f);
            var scrollbar = ItemGridScroll.verticalScrollbar;
            if (scrollbar != null)
            {
                var scrollbarRect = scrollbar.GetComponent<RectTransform>();
                scrollbarRect.SetParent(ItemGridScroll.transform, false);
                scrollbarRect.anchorMin = new Vector2(1f, 0f);
                scrollbarRect.anchorMax = Vector2.one;
                scrollbarRect.offsetMin = new Vector2(-(scrollbarWidth + scrollbarInset), ItemGridPadding);
                scrollbarRect.offsetMax = new Vector2(-scrollbarInset, -ItemGridPadding);
                scrollbarRect.SetAsLastSibling();
                foreach (var graphic in scrollbar.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = true;
            }
            // The grid places its rows explicitly, so disable the factory's list layout.
            var layout = ItemGridRoot.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.enabled = false;
            var fitter = ItemGridRoot.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            ItemGridRoot.anchorMin = new Vector2(0f, 1f);
            ItemGridRoot.anchorMax = new Vector2(1f, 1f);
            ItemGridRoot.pivot = new Vector2(0.5f, 1f);
            ItemGridRoot.anchoredPosition = Vector2.zero;
            ItemGridRoot.sizeDelta = new Vector2(0f, initialHeight);
        }

        private static void FitItemGrid()
        {
            if (ItemGridScroll == null || ItemGridRoot == null || ItemDockDivider == null) return;
            var grid = ItemGridScroll.GetComponent<RectTransform>();
            var gridTop = grid.TransformPoint(new Vector3(0f, grid.rect.yMax, 0f));
            var dividerTop = ItemDockDivider.TransformPoint(new Vector3(0f, ItemDockDivider.rect.yMax, 0f));
            var availableHeight = grid.InverseTransformPoint(gridTop).y - grid.InverseTransformPoint(dividerTop).y - ItemGridPadding;
            if (availableHeight <= 0f) return;

            var chromeHeight = ItemGridPadding * 2f + (ItemGridVisibleRows - 1) * ItemRowGap;
            var tileHeight = Mathf.Clamp((availableHeight - chromeHeight) / ItemGridVisibleRows, 1f, ItemTileHeight);
            var gridHeight = chromeHeight + ItemGridVisibleRows * tileHeight;
            var viewportHeight = Mathf.Max(chromeHeight, gridHeight - ItemGridBottomSafetyInset);
            var element = grid.GetComponent<LayoutElement>();
            if (!Mathf.Approximately(element.preferredHeight, viewportHeight))
            {
                element.minHeight = element.preferredHeight = viewportHeight;
                LayoutRebuilder.ForceRebuildLayoutImmediate(GuiContent);
            }

            for (var index = 0; index < ItemGridRows.Count; index++)
            {
                var row = ItemGridRows[index];
                row.anchoredPosition = new Vector2(0f, -(ItemGridPadding + index * (tileHeight + ItemRowGap)));
                row.sizeDelta = new Vector2(-2f * ItemGridPadding, tileHeight);
            }
            var rows = ItemGridRows.Count;
            var contentHeight = ItemGridPadding * 2f + rows * tileHeight + Mathf.Max(0, rows - 1) * ItemRowGap;
            // Content height follows the real row count: padding it up to the viewport height made the
            // scroll view 4px taller than its viewport, so a useless scrollbar showed on short lists
            // such as the favourites page.
            ItemGridRoot.sizeDelta = new Vector2(0f, Mathf.Max(contentHeight, 1f));
        }

        private static void RefreshItemGrid()
        {
            if (ItemGridRoot == null) return;
            for (var i = ItemGridRoot.childCount - 1; i >= 0; i--)
            {
                var child = ItemGridRoot.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            ItemTileViews.Clear();
            ItemGridRows.Clear();
            ItemTileHoveredId = null;

            if (ItemGridScroll != null) ItemGridScroll.StopMovement();
            ItemGridRoot.anchoredPosition = Vector2.zero;
            var filtered = new List<ItemEntry>();
            for (var i = 0; i < ItemEntries.Count; i++)
                if (ItemMatchesFilter(ItemEntries[i])) filtered.Add(ItemEntries[i]);

            var rowCount = Mathf.CeilToInt(filtered.Count / (float)ItemGridColumns);
            if (filtered.Count == 0)
            {
                if (ItemFavoritesPage && !HasCatalogFavorites())
                {
                    BuildItemFavoritesEmpty();
                    return;
                }
                var empty = NativeFlowText(ItemGridRoot, "ItemGridEmpty", Loc.T("No matching items"),
                    UITheme.SizeSmall, UITheme.InkSoft, TextAlignmentOptions.Center, false);
                empty.Rect.anchorMin = new Vector2(0f, 1f);
                empty.Rect.anchorMax = Vector2.one;
                empty.Rect.offsetMin = new Vector2(0f, -48f);
                empty.Rect.offsetMax = Vector2.zero;
                FitItemGrid();
                return;
            }

            for (var r = 0; r < rowCount; r++)
            {
                var row = UIF.Rect("ItemRow." + r, ItemGridRoot);
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0f, -(ItemGridPadding + r * (ItemTileHeight + ItemRowGap)));
                row.sizeDelta = new Vector2(0f, ItemTileHeight);
                ItemGridRows.Add(row);
                for (var c = 0; c < ItemGridColumns; c++)
                {
                    var index = r * ItemGridColumns + c;
                    if (index >= filtered.Count) break;
                    CreateItemTile(row, filtered[index], c);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(GuiContent);
            FitItemGrid();
            if (string.IsNullOrEmpty(ItemSelectedId) && string.IsNullOrEmpty(ItemCustomId))
                SelectItem(filtered[0]);
            else
                UpdateItemTileSelection();
        }

        private static bool ItemMatchesFilter(ItemEntry entry)
        {
            if (entry == null) return false;
            if (ItemFavoritesPage) return IsFavorite(entry.Id);
            if (!string.IsNullOrEmpty(ItemCategoryFilter) &&
                !string.Equals(entry.Category.ToString(), ItemCategoryFilter, StringComparison.Ordinal)) return false;
            if (string.IsNullOrWhiteSpace(ItemSearchText)) return true;
            var value = ItemSearchText.Trim();
            return Contains(entry.Name, value) || Contains(entry.Id, value) || Contains(ItemCategoryLabel(entry.Category.ToString()), value);
        }

        private static bool Contains(string source, string value)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CreateItemTile(Transform parent, ItemEntry entry, int column)
        {
            var stage = "panel-call";
            try
            {
                var panel = UIF.Panel(parent, "ItemTile." + entry.Id, UITheme.PaperDk, 10f, false, false);
                stage = "panel-root";
                if (panel == null)
                {
                    Log?.LogError("[MaydayHostControl] item panel is null: " + entry.Id);
                    return;
                }
                var visual = panel.Root;
                if (visual == null)
                {
                    Log?.LogError("[MaydayHostControl] item panel root is null: " + entry.Id);
                    return;
                }
                stage = "panel-layout";
                visual.anchorMin = new Vector2(column / (float)ItemGridColumns, 0f);
                visual.anchorMax = new Vector2((column + 1f) / ItemGridColumns, 1f);
                visual.offsetMin = new Vector2(column * ItemColumnGap / ItemGridColumns, 0f);
                visual.offsetMax = new Vector2(-(ItemGridColumns - 1 - column) * ItemColumnGap / ItemGridColumns, 0f);
                stage = "panel-image";
                var background = visual.GetComponent<Image>();
                if (background == null) background = visual.GetComponentInChildren<Image>();
                stage = "panel-ray";
                if (background != null) background.raycastTarget = false;

                stage = "icon";
                var sprite = GetItemIcon(entry.Id);
                if (sprite != null)
                {
                    var image = UIF.Image(visual, "Icon", sprite, Color.white, true);
                    image.raycastTarget = false;
                    image.preserveAspect = true;
                    PositionItemTileIcon(image.rectTransform);
                }
                else
                {
                    var initials = GetItemInitials(entry);
                    var fallback = NativeFlowText(visual, "IconFallback", initials, 18f, UITheme.InkSoft,
                        TextAlignmentOptions.Center, false);
                    NoRaycast(fallback);
                    PositionItemTileIcon(fallback.Rect);
                }

                stage = "hit";
                var hit = UIF.Rect("Hit", visual);
                UIF.Stretch(hit);
                UIF.IgnoreLayout(hit.gameObject);
                hit.SetAsFirstSibling();
                var hitGraphic = hit.gameObject.AddComponent<Image>();
                hitGraphic.color = Color.clear;
                hitGraphic.raycastTarget = true;
                var hitButton = hit.gameObject.AddComponent<Button>();
                hitButton.targetGraphic = hitGraphic;
                hitButton.transition = Selectable.Transition.None;
                hitButton.onClick.AddListener(new Action(() => SelectItem(entry)));

                stage = "name";
                var name = NativeFlowText(visual, "Name", entry.Name, UITheme.SizeSmall * 0.82f, UITheme.InkSoft,
                    TextAlignmentOptions.Center, false);
                NoRaycast(name);
                name.Rect.anchorMin = new Vector2(0f, 0.09f);
                name.Rect.anchorMax = new Vector2(1f, 0.38f);
                name.Rect.offsetMin = new Vector2(4f, 0f);
                name.Rect.offsetMax = new Vector2(-4f, 0f);
                var nameText = name.GetComponent<TMP_Text>();
                if (nameText != null)
                {
                    nameText.enableWordWrapping = false;
                    nameText.overflowMode = TextOverflowModes.Ellipsis;
                    nameText.richText = false;
                }

                stage = "check";
                var check = NativeFlowText(visual, "Check", "✓", 18f, UITheme.Teal, TextAlignmentOptions.Center, false);
                NoRaycast(check);
                UIF.IgnoreLayout(check.gameObject);
                UIF.At(check.Rect, new Vector2(1f, 1f), new Vector2(-4f, -8f), new Vector2(20f, 20f));
                check.gameObject.SetActive(false);

                stage = "favorite-star";
                var favorite = NativeFlowText(visual, "Favorite", "★", 16f, UITheme.Gold, TextAlignmentOptions.Center, false);
                NoRaycast(favorite);
                UIF.At(favorite.Rect, new Vector2(1f, 1f), new Vector2(-4f, -8f), new Vector2(22f, 22f));
                favorite.gameObject.SetActive(false);

                var stateOutline = background != null ? background.gameObject.AddComponent<Outline>() : null;
                if (stateOutline != null)
                {
                    stateOutline.effectColor = new Color(UITheme.Teal.r, UITheme.Teal.g, UITheme.Teal.b, 0.95f);
                    stateOutline.effectDistance = new Vector2(2f, -2f);
                    stateOutline.useGraphicAlpha = true;
                    stateOutline.enabled = false;
                }

                if (entry.Special)
                {
                    stage = "badge";
                    var badge = NativeFlowText(visual, "Special", Loc.T("Special"), UITheme.SizeTiny, UITheme.Orange,
                        TextAlignmentOptions.Left, false);
                    NoRaycast(badge);
                    UIF.IgnoreLayout(badge.gameObject);
                    UIF.At(badge.Rect, UIF.TopLeft, new Vector2(8f, -8f), new Vector2(72f, 18f));
                    if (badge.Tmp != null)
                    {
                        badge.Tmp.enableAutoSizing = true;
                        badge.Tmp.fontSizeMax = UITheme.SizeTiny;
                        badge.Tmp.fontSizeMin = Mathf.Max(9f, UITheme.SizeTiny * 0.75f);
                        badge.Tmp.enableWordWrapping = false;
                        badge.Tmp.overflowMode = TextOverflowModes.Ellipsis;
                    }
                }

                ItemTileViews[entry.Id] = new ItemTileView
                {
                    Bg = background,
                    Check = check.GetComponent<TMP_Text>(),
                    Favorite = favorite.GetComponent<TMP_Text>(),
                    Root = visual,
                    Name = nameText,
                    StateOutline = stateOutline,
                    Hit = hit.gameObject
                };
            }
            catch (Exception e)
            {
                Log?.LogError("[MaydayHostControl] item tile failed stage=" + stage + " id=" + entry.Id + " " + e);
                throw;
            }
        }

        private static void PositionItemTileIcon(RectTransform icon)
        {
            icon.anchorMin = new Vector2(0.5f, 0.39f);
            icon.anchorMax = new Vector2(0.5f, 0.95f);
            icon.offsetMin = new Vector2(-32f, 0f);
            icon.offsetMax = new Vector2(32f, 0f);
        }

        private static void SetItemTileOutline(ItemTileView view, bool visible)
        {
            if (view?.StateOutline != null) view.StateOutline.enabled = visible;
        }

        private static GameObject GetItemPointerHit()
        {
            var eventSystem = EventSystem.current;
            if (!GuiVisible || GuiTab != ItemsTabIndex || BlacklistWindow != null ||
                !Application.isFocused || eventSystem == null ||
                !NativeInput.TryGetCursorPosition(out var position) ||
                position.x < 0f || position.x >= Screen.width || position.y <= 0f || position.y > Screen.height)
                return null;

            if (ItemTilePointer == null || ItemTileEventSystem != eventSystem)
            {
                ItemTileEventSystem = eventSystem;
                ItemTilePointer = new PointerEventData(eventSystem);
                ItemTileRaycastHits = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
            }

            // Resolve the topmost UI hit even when the input module has not sent PointerExit.
            ItemTilePointer.position = position;
            eventSystem.RaycastAll(ItemTilePointer, ItemTileRaycastHits);
            GameObject hit = null;
            for (var i = 0; i < ItemTileRaycastHits.Count; i++)
            {
                hit = ItemTileRaycastHits[i].gameObject;
                if (hit != null) break;
            }
            ItemTileRaycastHits.Clear();
            return hit;
        }

        private static void RefreshItemPointerVisuals()
        {
            if (ItemHoverButtons.Count == 0 && (!GuiVisible || GuiTab != ItemsTabIndex)) return;
            var hit = GetItemPointerHit();
            string hoveredId = null;
            if (hit != null && ItemPopupRoot == null)
            {
                foreach (var pair in ItemTileViews)
                {
                    if (pair.Value.Hit != hit) continue;
                    hoveredId = pair.Key;
                    break;
                }
            }
            SetItemTileHover(hoveredId);

            var hoveredButton = hit != null ? hit.GetComponentInParent<UIButton>() : null;
            if (hoveredButton != null && !ItemHoverButtons.Contains(hoveredButton)) hoveredButton = null;
            var hoveredSelector = hit != null ? hit.GetComponentInParent<UISelector>() : null;
            if (hoveredSelector != ItemDeliveryDestSelector) hoveredSelector = null;

            var eventSystem = EventSystem.current;
            var selectedObject = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            // Native pointer enter can also take focus. Drop pointer-owned focus on exit,
            // without clearing keyboard focus on a different control.
            if (selectedObject != null &&
                ((ItemPointerButton != null && ItemPointerButton != hoveredButton && selectedObject == ItemPointerButton.gameObject) ||
                 (ItemPointerSelector != null && ItemPointerSelector != hoveredSelector && selectedObject == ItemPointerSelector.gameObject)))
            {
                eventSystem.SetSelectedGameObject(null);
                selectedObject = eventSystem.currentSelectedGameObject;
            }
            ItemPointerButton = hoveredButton;
            ItemPointerSelector = hoveredSelector;
            for (var i = ItemHoverButtons.Count - 1; i >= 0; i--)
            {
                var button = ItemHoverButtons[i];
                if (button == null)
                {
                    ItemHoverButtons.RemoveAt(i);
                    continue;
                }
                if (!button.isActiveAndEnabled) continue;
                var interactable = button.IsInteractable();
                var hovered = button == hoveredButton && interactable;
                var focused = selectedObject == button.gameObject && interactable;
                var highlightOnly = button == ItemQuantityMinusButton || button == ItemQuantityPlusButton ||
                    (ItemDeliveryDestSelector != null &&
                     (button == ItemDeliveryDestSelector.Prev || button == ItemDeliveryDestSelector.Next));
                if (highlightOnly)
                {
                    var state = !interactable ? Selectable.SelectionState.Disabled :
                        hovered ? Selectable.SelectionState.Highlighted :
                        focused ? Selectable.SelectionState.Selected : Selectable.SelectionState.Normal;
                    if (button.visual != state) button.DoStateTransition(state, true);
                }
                else if (!hovered && !focused && interactable)
                {
                    var restingState = button.selected ? Selectable.SelectionState.Selected : Selectable.SelectionState.Normal;
                    if (button.visual != restingState) button.DoStateTransition(restingState, true);
                }
                // Late pointer refresh must not restore the hover ring disabled by the panel style.
                if (button.Ring != null) button.Ring.enabled = false;
                if (IsItemChipButton(button)) RefreshItemChipPlate(button, hovered || focused);
            }

            if (ItemDeliveryDestSelector != null && ItemDeliveryDestSelector.Ring != null)
                ItemDeliveryDestSelector.Ring.enabled = false;
            RefreshDeliverySegmentHighlights(hit, hoveredButton);
        }

        private static void RefreshDeliverySegmentHighlights(GameObject hit, UIButton hoveredButton)
        {
            var input = ItemDeliveryQtyInput;
            var inputHovered = input != null && hit != null && hit.GetComponentInParent<TMP_InputField>() == input;
            SetDeliveryHighlight(DeliveryQuantityInputHighlight, inputHovered);
            SetDeliveryButtonHighlight(DeliveryQuantityMinusHighlight, ItemQuantityMinusButton, hoveredButton);
            SetDeliveryButtonHighlight(DeliveryQuantityPlusHighlight, ItemQuantityPlusButton, hoveredButton);
            var selector = ItemDeliveryDestSelector;
            SetDeliveryButtonHighlight(DeliveryDestinationPrevHighlight, selector != null ? selector.Prev : null, hoveredButton);
            SetDeliveryButtonHighlight(DeliveryDestinationNextHighlight, selector != null ? selector.Next : null, hoveredButton);
        }

        private static void SetDeliveryButtonHighlight(Image image, UIButton button, UIButton hoveredButton)
        {
            SetDeliveryHighlight(image, button != null && button == hoveredButton && button.IsInteractable());
        }

        private static void SetDeliveryHighlight(Image image, bool visible)
        {
            if (image != null && image.enabled != visible) image.enabled = visible;
        }

        private static Image BuildDeliveryHighlight(Image background, float start, float end)
        {
            if (background == null || end <= start) return null;
            var clip = UIF.Rect("SegmentHighlightClip", background.rectTransform);
            clip.anchorMin = new Vector2(start, 0f);
            clip.anchorMax = new Vector2(end, 1f);
            clip.offsetMin = clip.offsetMax = Vector2.zero;
            UIF.IgnoreLayout(clip.gameObject);
            clip.SetAsFirstSibling();
            clip.gameObject.AddComponent<RectMask2D>();

            // Draw the same full-sized plate; crop it at the segment's straight edges.
            var rect = UIF.Rect("SegmentHighlight", clip);
            var span = end - start;
            rect.anchorMin = new Vector2(-start / span, 0f);
            rect.anchorMax = new Vector2((1f - start) / span, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = background.overrideSprite;
            image.type = background.type;
            image.pixelsPerUnitMultiplier = background.pixelsPerUnitMultiplier;
            image.preserveAspect = background.preserveAspect;
            image.fillCenter = background.fillCenter;
            image.color = Color.Lerp(Color.clear, UITheme.Ink, ItemHoverDarken);
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private static void SetItemTileHover(string id)
        {
            if (string.Equals(ItemTileHoveredId, id, StringComparison.Ordinal)) return;
            var previousId = ItemTileHoveredId;
            ItemTileHoveredId = id;
            if (previousId != null && ItemTileViews.TryGetValue(previousId, out var previous))
                UpdateItemTileBackground(previousId, previous);
            if (id != null && ItemTileViews.TryGetValue(id, out var current))
                UpdateItemTileBackground(id, current);
        }

        private static bool IsItemTileSelected(string id) => string.IsNullOrEmpty(ItemCustomId) &&
            string.Equals(id, ItemSelectedId, StringComparison.Ordinal);

        private static void UpdateItemTileBackground(string id, ItemTileView view)
        {
            if (view.Bg == null) return;
            if (IsItemTileSelected(id))
                view.Bg.color = Color.Lerp(UITheme.PaperDk, UITheme.TealLt, 0.24f);
            else if (string.Equals(id, ItemTileHoveredId, StringComparison.Ordinal))
                view.Bg.color = Color.Lerp(UITheme.PaperDk, UITheme.Ink, ItemHoverDarken);
            else
                view.Bg.color = UITheme.PaperDk;
        }

        private static void UpdateItemTileSelection()
        {
            foreach (var pair in ItemTileViews)
            {
                var selected = IsItemTileSelected(pair.Key);
                UpdateItemTileBackground(pair.Key, pair.Value);
                SetItemTileOutline(pair.Value, selected);
                if (pair.Value.Check != null) pair.Value.Check.gameObject.SetActive(selected);
                if (pair.Value.Favorite != null)
                    pair.Value.Favorite.gameObject.SetActive(!selected && IsFavorite(pair.Key));
            }
        }

        private static void SelectItem(ItemEntry entry)
        {
            if (entry == null) return;
            ItemCustomId = null;
            ItemSelectedId = entry.Id;
            ItemSelectedEntry = entry;
            ItemSpecialArmed = false;
            if (ItemTileViews.TryGetValue(entry.Id, out var view) && view.Root != null)
                Il2CppMayday.UI.UITween.Punch(view.Root, 0.05f, 0.16f);
            UpdateItemTileSelection();
            UpdateItemDock();
        }

        private static void SelectCustomItem(string id)
        {
            ItemCustomId = id;
            ItemSelectedId = string.Empty;
            ItemSelectedEntry = null;
            ItemSpecialArmed = false;
            UpdateItemTileSelection();
            UpdateItemDock();
        }

        private static void BuildItemDock()
        {
            if (GuiCardRoot == null) return;

            var line = UIF.Rect("ItemDockDivider", GuiCardRoot);
            var lineImage = line.gameObject.AddComponent<Image>();
            lineImage.color = UITheme.WithAlpha(UITheme.Ink, 0.10f);
            lineImage.raycastTarget = false;
            UIF.At(line, UIF.TopLeft, new Vector2(32f, -724f), new Vector2(1116f, 1f));
            ItemDockDivider = line;

            ItemDockRoot = UIF.Rect("ItemDock", GuiCardRoot);
            UIF.At(ItemDockRoot, UIF.TopLeft, new Vector2(32f, -732f), new Vector2(1116f, 64f));
            var dock = ItemDockRoot;

            ItemDockIconRoot = UIF.Rect("ItemDockIcon", dock);
            UIF.At(ItemDockIconRoot, UIF.TopLeft, new Vector2(12f, -12f), new Vector2(40f, 40f));
            ItemDockIcon = ItemDockIconRoot.gameObject.AddComponent<Image>();
            ItemDockIcon.raycastTarget = false;
            ItemDockIcon.preserveAspect = true;
            ItemDockIcon.enabled = false;
            var fallback = NativeFlowText(ItemDockIconRoot, "Fallback", "?", 20f, UITheme.InkFaint,
                TextAlignmentOptions.Center, false);
            NoRaycast(fallback);
            UIF.At(fallback.Rect, UIF.TopLeft, Vector2.zero, new Vector2(40f, 40f));
            ItemDockIconFallback = fallback.GetComponent<TMP_Text>();

            ItemDockName = NativeText(dock, "ItemDockName", string.Empty, UITheme.SizeBody, UITheme.Ink,
                TextAlignmentOptions.Left, 64f, 4f, 216f, 30f, false).GetComponent<TMP_Text>();
            ItemDockMeta = NativeText(dock, "ItemDockMeta", string.Empty, UITheme.SizeTiny, UITheme.InkSoft,
                TextAlignmentOptions.Left, 64f, 34f, 216f, 20f, false).GetComponent<TMP_Text>();
            ItemDockName.enableAutoSizing = true;
            ItemDockName.fontSizeMax = UITheme.SizeBody;
            ItemDockName.fontSizeMin = UITheme.SizeSmall * 0.7f;
            ItemDockName.enableWordWrapping = false;
            ItemDockName.overflowMode = TextOverflowModes.Truncate;
            ItemDockMeta.enableAutoSizing = true;
            ItemDockMeta.fontSizeMax = UITheme.SizeTiny;
            ItemDockMeta.fontSizeMin = UITheme.SizeTiny * 0.7f;
            ItemDockMeta.enableWordWrapping = false;
            ItemDockMeta.overflowMode = TextOverflowModes.Truncate;

            ItemDockFavoriteButton = ItemTextButton(dock, "ItemDockFavorite", string.Empty, ButtonStyle.Ghost,
                292f, 12f, 36f, 30f, ToggleSelectedFavorite, 20f);
            if (ItemDockFavoriteButton.Ring != null) ItemDockFavoriteButton.Ring.enabled = false;
            SetPlayerActionColors(ItemDockFavoriteButton, new Color(0f, 0f, 0f, 0f), UITheme.InkFaint);
            BuildItemDockFavoriteGlyphs(dock);

            ItemDeliverySettingsButton = UIF.Button(GuiCardRoot, "ItemDeliverySettings", Loc.T("Delivery settings"),
                NativeAction(ShowItemDeliveryPopup), ButtonStyle.Ghost, 360f, 48f,
                new Il2CppSystem.Nullable<GlyphId>(GlyphId.Gear), -1f, -1f);
            UIF.At(ItemDeliverySettingsButton.GetComponent<RectTransform>(), UIF.TopLeft,
                new Vector2(48f, -802f), new Vector2(360f, 48f));
            ItemDeliverySettingsButton.hoverScale = 1f;
            TrackItemHoverButton(ItemDeliverySettingsButton);
            if (ItemDeliverySettingsButton.Bg != null) ItemDeliverySettingsButton.Bg.raycastTarget = true;
            // No plate behind this entry: the gear glyph and the summary text stay one control
            // and only their ink changes between idle and hover.
            var deliveryNoBg = new Color(0f, 0f, 0f, 0f);
            SetPlayerActionColors(ItemDeliverySettingsButton, deliveryNoBg, UITheme.InkSoft);
            ItemDeliverySettingsButton.bgHover = deliveryNoBg;
            ItemDeliverySettingsButton.bgPress = deliveryNoBg;
            ItemDeliverySettingsButton.bgSelected = deliveryNoBg;
            ItemDeliverySettingsButton.bgDisabled = deliveryNoBg;
            ItemDeliverySettingsButton.fgHover = UITheme.Ink;
            ItemDeliverySettingsButton.fgSelected = UITheme.InkSoft;
            ItemDeliverySettingsButton.Snap();
            ItemDeliverySummaryLabel = ItemDeliverySettingsButton.GetComponentInChildren<TMP_Text>();
            if (ItemDeliverySummaryLabel != null)
            {
                ItemDeliverySummaryLabel.enableAutoSizing = false;
                ItemDeliverySummaryLabel.fontSize = UITheme.SizeTiny;
                ItemDeliverySummaryLabel.enableWordWrapping = false;
                ItemDeliverySummaryLabel.overflowMode = TextOverflowModes.Ellipsis;
                ItemDeliverySummaryLabel.margin = new Vector4(0f, 0f, 4f, 0f);
                ItemDeliverySummaryLabel.raycastTarget = false;
            }

            ItemSpawnButton = ItemTextButton(dock, "ItemSpawn", Loc.T("Spawn {0}", ItemQuantity), ButtonStyle.Primary,
                936f, 9f, 180f, 46f, SpawnSelectedItem, UITheme.SizeSmall * 0.9f);
            SetPlayerActionColors(ItemSpawnButton, UITheme.Navy, Color.white);
            ItemSpawnButton.fgDisabled = UITheme.InkSoft;
            ItemSpawnButton.bgDisabled = UITheme.PaperDk;
        }

        private static void BuildItemDockFavoriteGlyphs(Transform dock)
        {
            // A font star sits low and right of its own box, so the dock favourite uses the game's
            // own star sprites, placed in a fixed square centred on the favourite button.
            const float iconSize = 20f;
            const float centerX = 292f + 18f;
            const float centerY = 12f + 15f;
            var topLeft = new Vector2(centerX - iconSize * 0.5f, -(centerY - iconSize * 0.5f));
            var size = new Vector2(iconSize, iconSize);

            ItemDockFavoriteEmptyIcon = UIF.Glyph(dock, "ItemDockFavoriteEmpty", GlyphId.StarEmpty,
                UITheme.InkFaint, iconSize);
            UIF.At(ItemDockFavoriteEmptyIcon.rectTransform, UIF.TopLeft, topLeft, size);
            ItemDockFavoriteEmptyIcon.raycastTarget = false;
            ItemDockFavoriteEmptyIcon.preserveAspect = true;

            ItemDockFavoriteFullIcon = UIF.Glyph(dock, "ItemDockFavoriteFull", GlyphId.Star,
                UITheme.Gold, iconSize);
            UIF.At(ItemDockFavoriteFullIcon.rectTransform, UIF.TopLeft, topLeft, size);
            ItemDockFavoriteFullIcon.raycastTarget = false;
            ItemDockFavoriteFullIcon.preserveAspect = true;
            ItemDockFavoriteFullIcon.gameObject.SetActive(false);
        }

        private static void UpdateItemDeliverySummary()
        {
            if (ItemDeliverySummaryLabel == null) return;
            var destination = ItemDestination == 1 ? Loc.T("Ground") : Loc.T("Backpack");
            var summary = Loc.T("{0} items", ItemQuantity) + " · " + destination + " · " + CurrentItemTargetName();
            ItemDeliverySummaryLabel.richText = false;
            ItemDeliverySummaryLabel.text = summary;
            if (ItemDeliverySettingsButton != null && ItemDeliverySettingsButton.Label != null)
                ItemDeliverySettingsButton.Label.Align = TextAlignmentOptions.Left;
        }

        private static void ShowItemDeliveryPopup()
        {
            if (ItemSearchInput != null) ItemSearchInput.DeactivateInputField();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            const float popupWidth = 560f;
            const float iconTop = 54f;
            const float iconSize = 104f;
            const float identityGap = 8f;
            const float idHeight = 24f;
            const float copyHeight = 32f;
            const float idTop = iconTop + iconSize + identityGap;
            var popupHeight = 504f;
            var panel = OpenItemPopup("ItemDeliveryPopup", popupWidth, popupHeight);

            const float pad = 24f;
            const float controlW = 256f;
            const float controlX = 280f;
            const float rowLeft = 24f;
            const float rowWidth = 512f;
            const float rowHeight = 56f;
            const float rowGap = 6f;

            var entry = GetSelectedItemEntry();
            var id = GetSelectedItemId();
            if (entry == null && !string.IsNullOrEmpty(id))
                entry = ItemEntries.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));

            var title = NativeText(panel, "DeliveryTitle", Loc.T("Delivery settings"), UITheme.SizeBody, UITheme.Ink,
                TextAlignmentOptions.Left, pad, 18f, 400f, 28f, false);
            NoRaycast(title);

            var hero = UIF.Panel(panel, "DeliveryHero", UITheme.PaperDk, 20f, false, false);
            var popupRect = panel.GetComponent<RectTransform>();
            UIF.At(hero.Root, UIF.TopLeft, new Vector2((popupWidth - iconSize) * 0.5f, -iconTop), new Vector2(iconSize, iconSize));
            var sprite = GetItemIcon(id);
            if (sprite != null)
            {
                var image = UIF.Image(hero.Root, "Icon", sprite, Color.white, true);
                image.raycastTarget = false;
                image.preserveAspect = true;
                UIF.At(image.rectTransform, UIF.TopLeft, new Vector2(6f, -6f), new Vector2(iconSize - 12f, iconSize - 12f));
            }
            else
            {
                var fallback = NativeText(hero.Root, "Fallback", GetDeliveryInitials(entry, id), 28f,
                    UITheme.InkFaint, TextAlignmentOptions.Center, 0f, 0f, iconSize, iconSize, false);
                NoRaycast(fallback);
            }

            var copyId = string.IsNullOrEmpty(id) ? null : id;
            var copyTop = idTop + idHeight + identityGap;
            if (copyId == null)
                NoRaycast(NativeText(panel, "DeliveryIdentity", Loc.T("No item selected"), 16f,
                    UITheme.Ink, TextAlignmentOptions.Center, pad, idTop, rowWidth, idHeight, false));

            ItemDeliveryCopyHint = null;
            ItemDeliveryCopyUntil = 0f;

            if (copyId != null)
            {
                var idValue = Loc.T("Item ID") + ": " + copyId;
                var idLabel = NativeText(panel, "DeliveryItemId", idValue,
                    16f, UITheme.Ink, TextAlignmentOptions.Center, pad, idTop, rowWidth, idHeight, true);
                var idText = idLabel.GetComponent<TMP_Text>();
                if (idText != null)
                {
                    idText.text = idValue;
                    idText.raycastTarget = false;
                    idText.richText = false;
                    idText.enableWordWrapping = true;
                    idText.enableAutoSizing = false;
                    idText.fontSize = 16f;
                    idText.overflowMode = TextOverflowModes.Overflow;
                    var measuredHeight = Mathf.Max(idHeight, Mathf.Ceil(idText.GetPreferredValues(idValue, rowWidth, float.PositiveInfinity).y));
                    UIF.At(idLabel.Rect, UIF.TopLeft, new Vector2(pad, -idTop), new Vector2(rowWidth, measuredHeight));
                    copyTop = idTop + measuredHeight + identityGap;
                }

                var copyButton = ItemTextButton(panel, "DeliveryCopyIdButton", Loc.T("Click to copy ID"),
                    ButtonStyle.Ghost, 0f, copyTop, 80f, copyHeight,
                    () => CopyDeliveryId(copyId), UITheme.SizeTiny);
                SetPlayerActionColors(copyButton, Color.clear, UITheme.TealDk);
                ItemDeliveryCopyHint = copyButton.GetComponentInChildren<TMP_Text>();
                var copyWidth = ItemButtonWidth(copyButton);
                if (ItemDeliveryCopyHint != null)
                {
                    var copiedWidth = ItemDeliveryCopyHint.GetPreferredValues(Loc.T("ID copied"), float.PositiveInfinity, float.PositiveInfinity).x;
                    copyWidth = Mathf.Max(copyWidth, Mathf.Ceil(copiedWidth + 32f));
                }
                PlaceItemButton(copyButton, (popupWidth - copyWidth) * 0.5f, copyTop, copyWidth, copyHeight);
            }

            var firstRowTop = (copyId != null ? copyTop + copyHeight : idTop + idHeight) + 12f;
            popupHeight = firstRowTop + rowHeight * 3f + rowGap * 2f + 70f;
            UIF.At(popupRect, UIF.Center, Vector2.zero, new Vector2(popupWidth, popupHeight));
            AddDeliveryRow(panel, "DeliveryTarget", Loc.T("Target player"), null, rowLeft, firstRowTop, rowWidth, rowHeight);
            ItemDeliveryTargetControl = panel;
            var targetLabel = NativeFlowText(panel, "DeliveryTargetValue",
                ItemTargets.Count > 0 ? CurrentItemTargetName() : Loc.T("No target"),
                UITheme.SizeSmall, ItemTargets.Count > 0 ? UITheme.Ink : UITheme.InkSoft,
                TextAlignmentOptions.Center, false);
            NoRaycast(targetLabel);

            ItemDeliveryTargetLabel = targetLabel.GetComponent<TMP_Text>();
            if (ItemDeliveryTargetLabel != null)
            {
                ItemDeliveryTargetLabel.enableAutoSizing = false;
                ItemDeliveryTargetLabel.fontSize = UITheme.SizeSmall;
                ItemDeliveryTargetLabel.enableWordWrapping = true;
                ItemDeliveryTargetLabel.richText = false;
                ItemDeliveryTargetLabel.overflowMode = TextOverflowModes.Overflow;
                ItemDeliveryTargetLabel.margin = new Vector4(8f, 0f, 8f, 0f);
                ItemDeliveryTargetLabel.alignment = TextAlignmentOptions.CenterGeoAligned;
            }
            RectTransform targetControl;
            var hasTargetChoices = ItemTargets.Count > 1;
            if (hasTargetChoices)
            {
                ItemDeliveryTargetButton = ItemTextButton(panel, "DeliveryTargetButton", string.Empty,
                    ButtonStyle.Secondary, controlX, firstRowTop + 8f, controlW, 40f,
                    ShowDeliveryTargetPopup, UITheme.SizeSmall);
                if (ItemDeliveryTargetButton.Label != null) ItemDeliveryTargetButton.Label.gameObject.SetActive(false);
                SetPlayerActionColors(ItemDeliveryTargetButton, UITheme.PaperDk, UITheme.Ink);
                targetControl = ItemDeliveryTargetButton.GetComponent<RectTransform>();
            }
            else
            {
                targetControl = UIF.Panel(panel, "DeliveryTargetStatic", UITheme.PaperDk, 20f, false, false).Root;
                UIF.At(targetControl, UIF.TopLeft, new Vector2(controlX, -firstRowTop - 8f), new Vector2(controlW, 40f));
            }
            targetLabel.Rect.SetParent(targetControl, false);
            UIF.IgnoreLayout(targetLabel.gameObject);
            UIF.Stretch(targetLabel.Rect);

            AddDeliveryRow(panel, "DeliveryQuantity", Loc.T("Quantity"), null, rowLeft, firstRowTop + rowHeight + rowGap, rowWidth, rowHeight);
            var qtyGroup = UIF.Panel(panel, "DeliveryQtyGroup", UITheme.PaperDk, 20f, false, false);
            UIF.At(qtyGroup.Root, UIF.TopLeft, new Vector2(controlX, -firstRowTop - rowHeight - rowGap - 8f), new Vector2(controlW, 40f));
            const float stepWidth = 48f;
            const float stepFraction = stepWidth / controlW;
            var quantityBackground = qtyGroup.Root.GetComponent<Image>() ?? qtyGroup.Root.GetComponentInChildren<Image>();
            DeliveryQuantityMinusHighlight = BuildDeliveryHighlight(quantityBackground, 0f, stepFraction);
            DeliveryQuantityInputHighlight = BuildDeliveryHighlight(quantityBackground, stepFraction, 1f - stepFraction);
            DeliveryQuantityPlusHighlight = BuildDeliveryHighlight(quantityBackground, 1f - stepFraction, 1f);
            ItemQuantityMinusButton = ItemTextButton(qtyGroup.Root, "DeliveryQtyMinus", "−", ButtonStyle.Ghost,
                0f, 0f, stepWidth, 40f, () => AdjustItemQuantity(-1), 20f);
            ItemQuantityPlusButton = ItemTextButton(qtyGroup.Root, "DeliveryQtyPlus", "+", ButtonStyle.Ghost,
                controlW - stepWidth, 0f, stepWidth, 40f, () => AdjustItemQuantity(1), 20f);
            StyleQuantityStepButton(ItemQuantityMinusButton);
            StyleQuantityStepButton(ItemQuantityPlusButton);
            UpdateQuantityStepButtons();
            var quantityInputHost = UIF.Rect("DeliveryQtyInputHost", qtyGroup.Root);
            UIF.At(quantityInputHost, UIF.TopLeft, new Vector2(stepWidth, 0f), new Vector2(controlW - stepWidth * 2f, 40f));
            ItemDeliveryQtyInput = UIF.Input(quantityInputHost, "DeliveryQtyInput", ItemQuantity.ToString(), "1", 48,
                NativeStringAction(value =>
                {
                    if (!int.TryParse(value, out var parsed)) return;
                    ItemQuantity = Mathf.Clamp(parsed, 1, 99);
                    UpdateQuantityStepButtons();
                    UpdateItemDock();
                }), controlW - stepWidth * 2f, 40f);
            UIF.Stretch(ItemDeliveryQtyInput.GetComponent<RectTransform>());
            ItemDeliveryQtyInput.pointSize = UITheme.SizeSmall;
            var qtyText = ItemDeliveryQtyInput.textComponent;
            if (qtyText != null)
            {
                qtyText.enableAutoSizing = false;
                qtyText.alignment = TextAlignmentOptions.Center;
            }
            ItemDeliveryQtyInput.caretColor = UITheme.Ink;
            // UIF installs its own selection effect, which can re-enable the ring
            // independently of TMP's transition when the input receives focus.
            ItemDeliveryQtyInput.transition = Selectable.Transition.None;
            foreach (var selectionEffect in quantityInputHost.GetComponentsInChildren<UISelectFx>(true))
            {
                selectionEffect.enabled = false;
                var ring = selectionEffect.ring;
                if (ring == null) continue;
                ring.raycastTarget = false;
                ring.enabled = false;
            }
            foreach (var decoration in quantityInputHost.GetComponentsInChildren<Image>(true))
            {
                decoration.raycastTarget = false;
                decoration.enabled = false;
            }
            foreach (var border in quantityInputHost.GetComponentsInChildren<Outline>(true)) border.enabled = false;
            var inputHitRect = UIF.Rect("DeliveryQtyInputHit", ItemDeliveryQtyInput.transform);
            UIF.Stretch(inputHitRect);
            UIF.IgnoreLayout(inputHitRect.gameObject);
            inputHitRect.SetAsFirstSibling();
            var inputHitImage = inputHitRect.gameObject.AddComponent<Image>();
            inputHitImage.color = Color.clear;
            inputHitImage.raycastTarget = true;
            AddDeliverySeparator(qtyGroup.Root, stepWidth);
            AddDeliverySeparator(qtyGroup.Root, controlW - stepWidth);

            AddDeliveryRow(panel, "DeliveryDest", Loc.T("Destination"), null, rowLeft, firstRowTop + (rowHeight + rowGap) * 2f, rowWidth, rowHeight);
            ItemDeliveryDestinationControl = panel;
            UISelector destinationSelector = null;
            destinationSelector = UIF.Selector(ItemDeliveryDestinationControl, "DeliveryDestSelector",
                new Il2CppStringArray(new[] { Loc.T("Backpack"), Loc.T("Ground") }),
                ItemDestination, NativeIntAction(value =>
                {
                    ItemDestination = value == 1 ? 1 : 0;
                    UpdateItemDock();
                    StyleItemSelector(destinationSelector);
                }), controlW);
            ItemDeliveryDestSelector = destinationSelector;
            UIF.At(destinationSelector.GetComponent<RectTransform>(), UIF.TopLeft,
                new Vector2(controlX, -firstRowTop - (rowHeight + rowGap) * 2f - 8f), new Vector2(controlW, 40f));
            var destinationBackground = destinationSelector.Bg;
            var previousButton = destinationSelector.Prev;
            var nextButton = destinationSelector.Next;
            var previousRect = previousButton != null ? previousButton.GetComponent<RectTransform>() : null;
            var nextRect = nextButton != null ? nextButton.GetComponent<RectTransform>() : null;
            StyleItemSelector(destinationSelector);
            if (destinationBackground != null && previousRect != null && nextRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(destinationSelector.GetComponent<RectTransform>());
                var plate = destinationBackground.rectTransform;
                var previousInnerEdge = previousRect.TransformPoint(new Vector3(previousRect.rect.xMax, 0f, 0f));
                var nextInnerEdge = nextRect.TransformPoint(new Vector3(nextRect.rect.xMin, 0f, 0f));
                var leftEnd = Mathf.InverseLerp(plate.rect.xMin, plate.rect.xMax, plate.InverseTransformPoint(previousInnerEdge).x);
                var rightStart = Mathf.InverseLerp(plate.rect.xMin, plate.rect.xMax, plate.InverseTransformPoint(nextInnerEdge).x);
                DeliveryDestinationPrevHighlight = BuildDeliveryHighlight(destinationBackground, 0f, leftEnd);
                DeliveryDestinationNextHighlight = BuildDeliveryHighlight(destinationBackground, rightStart, 1f);
            }

            var done = ItemTextButton(panel, "DeliveryDone", Loc.T("Done"), ButtonStyle.Primary,
                0f, popupHeight - 56f, 96f, 40f, CloseItemPopup, UITheme.SizeSmall);
            var doneWidth = ItemButtonWidth(done, 96f);
            PlaceItemButton(done, (popupWidth - doneWidth) * 0.5f, popupHeight - 56f, doneWidth, 40f);
            SetPlayerActionColors(done, UITheme.Navy, Color.white);
        }

        private static string DeliveryColor(Color color, string value)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + value + "</color>";
        }

        private static string GetDeliveryInitials(ItemEntry entry, string id)
        {
            var key = entry != null ? entry.Id : id;
            if (!string.IsNullOrEmpty(key))
            {
                try
                {
                    var initials = Il2CppMayday.UI.ItemIcons.Initials(key);
                    if (!string.IsNullOrWhiteSpace(initials)) return initials;
                }
                catch { }
            }
            if (entry != null && !string.IsNullOrEmpty(entry.Name)) return entry.Name.Substring(0, 1);
            if (!string.IsNullOrEmpty(key)) return key.Substring(0, 1).ToUpperInvariant();
            return "?";
        }

        private static void StyleItemSelector(UISelector selector)
        {
            if (selector == null) return;
            try
            {
                var value = selector.ValueLabel;
                if (value != null)
                {
                    if (value.Tmp != null)
                    {
                        value.Tmp.enableAutoSizing = false;
                        value.Tmp.fontSize = UITheme.SizeSmall;
                        value.Tmp.enableWordWrapping = true;
                        value.Tmp.overflowMode = TextOverflowModes.Overflow;
                    }
                }
                StyleDeliveryArrow(selector.Prev);
                StyleDeliveryArrow(selector.Next);
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] delivery selector style failed: " + e.Message);
            }
        }

        private static void EnsureDeliverySelectorStyles()
        {
            EnsureDeliverySelectorStyle(ItemDeliveryDestSelector);
            if (ItemDeliveryTargetLabel != null && ItemTargets.Count > 0)
                ItemDeliveryTargetLabel.text = CurrentItemTargetName();
        }

        private static void EnsureDeliverySelectorStyle(UISelector selector)
        {
            if (selector == null) return;
            var value = selector.ValueLabel;
            if (value == null) return;
            if (value.Tmp != null && value.Tmp.fontStyle != FontStyles.Normal) StyleItemSelector(selector);
        }

        private static void StyleDeliveryArrow(UIButton button)
        {
            if (button == null) return;
            TrackItemHoverButton(button);
            button.hoverScale = 1f;
            button.bgNormal = Color.clear;
            button.bgHover = Color.Lerp(Color.clear, UITheme.Ink, ItemHoverDarken);
            button.bgPress = Color.Lerp(Color.clear, UITheme.Ink, 0.10f);
            button.bgSelected = Color.clear;
            button.bgDisabled = Color.clear;
            button.fgHover = UITheme.InkSoft;
            button.fgSelected = UITheme.InkSoft;
            if (button.Ring != null) button.Ring.enabled = false;
            var outline = button.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            button.Snap();
            var label = button.Label;
            if (label != null && !string.IsNullOrEmpty(label.Text))
            {
                label.Size = 17f;
                label.Color = UITheme.InkSoft;
                if (label.Tmp != null)
                {
                    label.Tmp.enableAutoSizing = false;
                    label.Tmp.fontStyle = FontStyles.Normal;
                }
            }
            var icon = button.Icon;
            if (icon != null)
            {
                icon.color = UITheme.InkSoft;
                if (icon.rectTransform != null)
                    icon.rectTransform.sizeDelta = new Vector2(18f, 18f);
            }
        }

        private static void AddDeliveryRow(Transform panel, string name, string title, string description,
            float x, float top, float width, float height)
        {
            var control = Il2CppMayday.UI.SettingsScreen.Row(panel, title, description, height);
            var row = control.parent;
            row.name = name;
            UIF.At(row.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(x, -top), new Vector2(width, height));
        }

        private static void AddDeliverySeparator(Transform parent, float x)
        {
            var line = UIF.Rect("DeliveryQtySeparator", parent);
            var image = line.gameObject.AddComponent<Image>();
            image.color = UITheme.WithAlpha(UITheme.Ink, 0.12f);
            image.raycastTarget = false;
            UIF.At(line, UIF.TopLeft, new Vector2(x, -8f), new Vector2(1f, 24f));
        }

        private static void CopyDeliveryId(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            try
            {
                GUIUtility.systemCopyBuffer = id;
                if (ItemDeliveryCopyHint != null)
                {
                    ItemDeliveryCopyHint.text = Loc.T("ID copied");
                    ItemDeliveryCopyHint.color = UITheme.TealDk;
                }
                ItemDeliveryCopyUntil = Time.unscaledTime + 2f;
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] copy item id failed: " + e.Message);
            }
        }

        private static void ResetDeliveryCopyHint()
        {
            ItemDeliveryCopyUntil = 0f;
            if (ItemDeliveryCopyHint == null) return;
            ItemDeliveryCopyHint.text = Loc.T("Click to copy ID");
            ItemDeliveryCopyHint.color = UITheme.InkSoft;
        }

        private static string CurrentItemTargetName()
        {
            var target = ItemTargets.Find(x => x.Key == SelectedGrantSteamId);
            return target != null ? target.Name : Loc.T("No target");
        }

        private static void BuildItemTargetCaption(Transform dock)
        {
            ItemTargetCaptionButton = null;
            ItemTargetCaptionLabel = null;
            var name = CurrentItemTargetName();
            var multiple = ItemTargets.Count > 1;
            var color = ItemTargets.Count == 0 ? UITheme.Red : UITheme.InkSoft;
            var text = name + (multiple ? "  >" : string.Empty);
            var label = NativeFlowText(dock, "ItemTargetCaption", text, 15f, color,
                TextAlignmentOptions.Left, false);
            NoRaycast(label);
            UIF.At(label.Rect, UIF.TopLeft, new Vector2(832f, -20f), new Vector2(92f, 24f));
            ItemTargetCaptionLabel = label.GetComponent<TMP_Text>();
            if (ItemTargetCaptionLabel != null)
            {
                ItemTargetCaptionLabel.enableWordWrapping = false;
                ItemTargetCaptionLabel.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (!multiple) return;
            var hit = UIF.Button(dock, "ItemTargetCaptionHit", string.Empty, NativeAction(ShowDeliveryTargetPopup),
                ButtonStyle.Ghost, 92f, 24f, new Il2CppSystem.Nullable<GlyphId>(), -1f, -1f);
            if (hit.Bg != null)
            {
                hit.Bg.color = new Color(1f, 1f, 1f, 0f);
                hit.Bg.raycastTarget = true;
            }
            hit.SetColors(new Color(0f, 0f, 0f, 0f),
                Color.Lerp(Color.clear, UITheme.Ink, ItemHoverDarken),
                Color.Lerp(Color.clear, UITheme.Ink, 0.10f),
                new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f));
            hit.Snap();
            UIF.At(hit.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(832f, -20f), new Vector2(92f, 24f));
            ItemTargetCaptionButton = hit;
        }

        private static void RefreshItemTargetCaption()
        {
            if (ItemTargetCaptionLabel != null)
            {
                var multiple = ItemTargets.Count > 1;
                ItemTargetCaptionLabel.text = CurrentItemTargetName() + (multiple ? "  >" : string.Empty);
                var color = ItemTargets.Count == 0 ? UITheme.Red : UITheme.InkSoft;
                ItemTargetCaptionLabel.color = color;
                if (ItemTargetCaptionButton != null)
                    SetPlayerActionColors(ItemTargetCaptionButton, new Color(0f, 0f, 0f, 0f), color);
            }
            if (ItemSpawnButton != null)
                ItemSpawnButton.SetInteractable(ItemTargets.Count > 0 &&
                    (ItemSelectedEntry != null || !string.IsNullOrEmpty(ItemCustomId)));
        }

        private static void CloseItemPopup()
        {
            if (ItemPopupRoot != null)
            {
                UnityEngine.Object.Destroy(ItemPopupRoot);
                ItemPopupRoot = null;
            }
            ItemIdPopupInput = null;
            ItemDeliveryQtyInput = null;
            DeliveryQuantityMinusHighlight = null;
            DeliveryQuantityInputHighlight = null;
            DeliveryQuantityPlusHighlight = null;
            DeliveryDestinationPrevHighlight = null;
            DeliveryDestinationNextHighlight = null;
            ItemDeliveryCopyHint = null;
            ItemDeliveryCopyUntil = 0f;
            ItemDeliveryTargetButton = null;
            ItemDeliveryTargetLabel = null;
            ItemDeliveryDestSelector = null;
        }

        private static Transform OpenItemPopup(string name, float width, float height)
        {
            CloseItemPopup();
            var overlay = UIF.Rect(name + "Overlay", GuiCardRoot);
            UIF.At(overlay, UIF.Center, Vector2.zero, new Vector2(1180f, 900f));
            var dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.07f, 0.10f, 0.45f);
            dim.raycastTarget = true;
            ItemPopupRoot = overlay.gameObject;

            var panel = UIF.Panel(overlay, name + "Panel", UITheme.Paper, 20f, true, true);
            UIF.At(panel.Root, UIF.Center, Vector2.zero, new Vector2(width, height));
            var close = UIF.IconButton(panel.Root, name + "Close", GlyphId.Cross, NativeAction(CloseItemPopup),
                ButtonStyle.Ghost, 32f);
            UIF.At(close.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(width - 56f, -16f), new Vector2(32f, 32f));
            TrackItemHoverButton(close);
            SetPlayerActionColors(close, new Color(0f, 0f, 0f, 0f), UITheme.InkSoft);
            return panel.Root;
        }

        private static void ShowDeliveryTargetPopup()
        {
            if (ItemSearchInput != null) ItemSearchInput.DeactivateInputField();
            const float width = 440f;
            const float rowHeight = 52f;
            var visibleRows = Mathf.Clamp(ItemTargets.Count, 1, 8);
            var height = 88f + visibleRows * rowHeight;
            var panel = OpenItemPopup("DeliveryTargetPopup", width, height);
            var title = NativeFlowText(panel, "DeliveryTargetPopupTitle", Loc.T("Select target player"),
                UITheme.SizeBody, UITheme.Ink, TextAlignmentOptions.Left, false);
            NoRaycast(title);
            UIF.At(title.Rect, UIF.TopLeft, new Vector2(24f, -16f), new Vector2(360f, 32f));
            var scroll = UIF.Scroll(panel, "DeliveryTargetScroll", out var content, pad: UIF.Pad(0));
            UIF.At(scroll.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(24f, -64f), new Vector2(width - 48f, height - 88f));
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            UIF.At(content, UIF.TopLeft, Vector2.zero, new Vector2(width - 48f, ItemTargets.Count * 52f));
            for (var i = 0; i < ItemTargets.Count; i++)
            {
                var target = ItemTargets[i];
                var selected = target.Key == SelectedGrantSteamId;
                var row = UIF.Button(content, "DeliveryTargetRow" + i, target.Name,
                    NativeAction(() =>
                    {
                        SelectedGrantSteamId = target.Key;
                        if (ItemDeliveryTargetLabel != null) ItemDeliveryTargetLabel.text = CurrentItemTargetName();
                        if (ItemDeliveryTargetButton != null)
                        {
                            var canChooseTarget = ItemTargets.Count > 1;
                            ItemDeliveryTargetButton.SetInteractable(canChooseTarget);
                            SetPlayerActionColors(ItemDeliveryTargetButton,
                                canChooseTarget ? UITheme.PaperDk : new Color(0f, 0f, 0f, 0f),
                                canChooseTarget ? UITheme.Ink : UITheme.InkSoft);
                        }
                        RefreshItemTargetCaption();
                        UpdateItemDock();
                        CloseItemPopup();
                    }), ButtonStyle.Secondary, width - 48f, 44f,
                    new Il2CppSystem.Nullable<GlyphId>(), -1f, -1f);
                UIF.At(row.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(0f, -(i * 52f)), new Vector2(width - 48f, 44f));
                if (selected)
                {
                    var check = NativeFlowText(row.transform, "SelectedCheck", "✓", UITheme.SizeSmall,
                        UITheme.TealDk, TextAlignmentOptions.Center, false);
                    NoRaycast(check);
                    UIF.At(check.Rect, UIF.TopLeft, new Vector2(8f, -8f), new Vector2(24f, 28f));
                }
                var rowText = row.GetComponentInChildren<TMP_Text>();
                if (rowText != null)
                {
                    rowText.margin = new Vector4(32f, 0f, 8f, 0f);
                    rowText.enableAutoSizing = false;
                    rowText.fontSize = UITheme.SizeSmall;
                    rowText.richText = false;
                    rowText.enableWordWrapping = true;
                    rowText.overflowMode = TextOverflowModes.Overflow;
                }
                TrackItemHoverButton(row);
                SetPlayerActionColors(row, selected ? Color.Lerp(UITheme.PaperDk, UITheme.Ink, 0.08f) : UITheme.PaperDk,
                    selected ? UITheme.Ink : UITheme.Ink);
            }
        }

        private static void ShowItemIdPopup()
        {
            if (ItemSearchInput != null) ItemSearchInput.DeactivateInputField();
            var panel = OpenItemPopup("ItemIdPopup", 440f, 232f);
            var title = NativeFlowText(panel, "ItemIdPopupTitle", Loc.T("Spawn by ID"), UITheme.SizeBody,
                UITheme.Ink, TextAlignmentOptions.Left, false);
            NoRaycast(title);
            UIF.At(title.Rect, UIF.TopLeft, new Vector2(24f, -16f), new Vector2(360f, 32f));
            ItemIdPopupInput = UIF.Input(panel, "ItemIdPopupInput", string.Empty, Loc.T("Enter item ID"), 64,
                NativeStringAction(_ => { }), 392f, 44f);
            UIF.At(ItemIdPopupInput.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(24f, -66f), new Vector2(392f, 44f));
            ItemIdPopupInput.pointSize = UITheme.SizeSmall;
            if (ItemIdPopupInput.textComponent != null) ItemIdPopupInput.textComponent.fontSize = UITheme.SizeSmall;
            var use = ItemTextButton(panel, "ItemIdPopupUse", Loc.T("Use"), ButtonStyle.Primary,
                24f, 132f, 180f, 44f, () =>
                {
                    var id = ItemIdPopupInput != null ? ItemIdPopupInput.text : string.Empty;
                    ApplyItemId(id);
                    CloseItemPopup();
                }, 18f);
            var cancel = ItemTextButton(panel, "ItemIdPopupCancel", Loc.T("Cancel"), ButtonStyle.Ghost,
                220f, 132f, 180f, 44f, CloseItemPopup, 18f);
            var useWidth = ItemButtonWidth(use, 80f);
            var cancelWidth = ItemButtonWidth(cancel, 80f);
            const float gap = 16f;
            var start = (440f - useWidth - cancelWidth - gap) * 0.5f;
            PlaceItemButton(use, start, 132f, useWidth, 44f);
            PlaceItemButton(cancel, start + useWidth + gap, 132f, cancelWidth, 44f);
            SetPlayerActionColors(cancel, Color.clear, UITheme.InkSoft);
            if (ItemIdPopupInput != null) ItemIdPopupInput.ActivateInputField();
        }



        private static void DestroyItemDock()
        {
            if (ItemDockRoot != null)
            {
                ItemDockRoot.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(ItemDockRoot.gameObject);
            }
            if (ItemDockDivider != null)
            {
                ItemDockDivider.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(ItemDockDivider.gameObject);
            }
            // The delivery-settings entry lives on the shared card root, so it must be torn down
            // with the dock; otherwise it stays visible on every other tab.
            if (ItemDeliverySettingsButton != null)
            {
                ItemDeliverySettingsButton.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(ItemDeliverySettingsButton.gameObject);
            }
            ItemDockRoot = null;
            ItemDockDivider = null;
            ItemDockIconRoot = null;
            ItemDockIcon = null;
            ItemDockIconFallback = null;
            ItemDockName = null;
            ItemDockMeta = null;
            ItemQuantityText = null;
            ItemQuantityInput = null;
            ItemTargetCaptionLabel = null;
            ItemTargetCaptionButton = null;
            ItemDeliverySummaryLabel = null;
            ItemDeliveryQtyInput = null;
            ItemDockFavoriteButton = null;
            ItemDockFavoriteEmptyIcon = null;
            ItemDockFavoriteFullIcon = null;
            ItemDeliverySettingsButton = null;
            ItemDestHighlight = null;
            ItemSpawnButton = null;
            ItemBackpackButton = null;
            ItemGroundButton = null;
        }

        private static void StyleQuantityStepButton(UIButton button)
        {
            if (button == null) return;
            var clear = new Color(0f, 0f, 0f, 0f);
            button.bgNormal = clear;
            button.bgHover = Color.Lerp(clear, UITheme.Ink, ItemHoverDarken);
            button.bgPress = clear;
            button.bgSelected = clear;
            button.bgDisabled = clear;
            button.fgNormal = UITheme.Ink;
            button.fgHover = UITheme.Ink;
            button.fgSelected = UITheme.Ink;
            button.fgDisabled = UITheme.InkSoft;
            button.transition = Selectable.Transition.None;
            button.Snap();
        }

        private static void UpdateQuantityStepButtons()
        {
            if (ItemQuantityMinusButton != null) ItemQuantityMinusButton.SetInteractable(ItemQuantity > 1);
            if (ItemQuantityPlusButton != null) ItemQuantityPlusButton.SetInteractable(ItemQuantity < 99);
        }


        private static void AdjustItemQuantity(int delta)
        {
            ItemQuantity = Mathf.Clamp(ItemQuantity + delta, 1, 99);
            UpdateQuantityStepButtons();
            UpdateItemDock();
        }

        private static void UpdateItemSpawnLabel()
        {
            if (ItemSpawnButton == null) return;
            var spawnText = ItemSpawnButton.GetComponentInChildren<TMP_Text>();
            if (spawnText == null) return;
            spawnText.text = ItemSpecialArmed ? Loc.T("Click again to confirm") : Loc.T("Spawn {0}", ItemQuantity);
            var width = ItemButtonWidth(ItemSpawnButton, 144f, 40f);
            PlaceItemButton(ItemSpawnButton, 1116f - width, 9f, width, 46f);
        }

        private static void SetItemDestination(int destination)
        {
            ItemDestination = destination == 1 ? 1 : 0;
            UpdateItemDock();
        }

        private static void UpdateDestinationButtons()
        {
            var clear = new Color(0f, 0f, 0f, 0f);
            var onBag = ItemDestination == 0;
            if (ItemDestHighlight != null)
                UIF.At(ItemDestHighlight, UIF.TopLeft, new Vector2(onBag ? 730f : 820f, -19f), new Vector2(88f, 26f));
            if (ItemBackpackButton != null)
                SetPlayerActionColors(ItemBackpackButton, clear, onBag ? UITheme.TealDk : UITheme.Ink);
            if (ItemGroundButton != null)
                SetPlayerActionColors(ItemGroundButton, clear, onBag ? UITheme.Ink : UITheme.TealDk);
        }

        private static ItemEntry GetSelectedItemEntry()
        {
            if (!string.IsNullOrEmpty(ItemCustomId)) return null;
            if (ItemSelectedEntry != null && string.Equals(ItemSelectedEntry.Id, ItemSelectedId, StringComparison.Ordinal))
                return ItemSelectedEntry;
            for (var i = 0; i < ItemEntries.Count; i++)
                if (string.Equals(ItemEntries[i].Id, ItemSelectedId, StringComparison.Ordinal)) return ItemEntries[i];
            return null;
        }

        private static string GetSelectedItemId()
        {
            return !string.IsNullOrEmpty(ItemCustomId) ? ItemCustomId : (GetSelectedItemEntry() != null ? ItemSelectedId : string.Empty);
        }

        private static void UpdateItemDock()
        {
            var entry = GetSelectedItemEntry();
            var custom = ItemCustomId;
            var hasItem = custom != null || entry != null;
            var special = custom != null || (entry != null && entry.Special);

            if (ItemDockName != null)
                ItemDockName.text = custom != null ? Loc.T("Custom item") : (entry != null ? entry.Name : Loc.T("No item selected"));
            if (ItemDockName != null)
                ItemDockName.color = UITheme.Ink;

            if (ItemDockIcon != null)
            {
                var sprite = custom != null ? null : GetItemIcon(entry != null ? entry.Id : string.Empty);
                ItemDockIcon.sprite = sprite;
                ItemDockIcon.enabled = sprite != null;
            }
            if (ItemDockIconFallback != null)
                ItemDockIconFallback.gameObject.SetActive(ItemDockIcon == null || ItemDockIcon.sprite == null);

            var favorite = entry != null && IsFavorite(entry.Id);
            if (ItemDockFavoriteButton != null)
            {
                // The favourite glyph is the centred sprite pair built in BuildItemDock; the button's
                // own text label stays empty so no second, off-centre star is drawn underneath it.
                if (ItemDockFavoriteEmptyIcon != null)
                    ItemDockFavoriteEmptyIcon.gameObject.SetActive(!favorite);
                if (ItemDockFavoriteFullIcon != null)
                    ItemDockFavoriteFullIcon.gameObject.SetActive(favorite);
                ItemDockFavoriteButton.SetInteractable(entry != null);
                SetPlayerActionColors(ItemDockFavoriteButton, new Color(0f, 0f, 0f, 0f),
                    favorite ? UITheme.Gold : UITheme.InkFaint);
            }

            UpdateItemSpawnLabel();
            UpdateItemDeliverySummary();
            if (ItemDeliveryQtyInput != null && !ItemDeliveryQtyInput.isFocused)
                ItemDeliveryQtyInput.SetTextWithoutNotify(ItemQuantity.ToString());
            UpdateItemDockMeta(entry, custom, special);

            if (ItemSpawnButton != null)
                ItemSpawnButton.SetInteractable(hasItem && ItemTargets.Count > 0);
        }

        private static void UpdateItemDockMeta(ItemEntry entry, string custom, bool special)
        {
            if (ItemDockMeta == null) return;
            if (!string.IsNullOrEmpty(ItemStatus) && Time.unscaledTime < ItemStatusUntil)
            {
                ItemDockMeta.text = ItemStatus;
                ItemDockMeta.color = ItemStatusError ? UITheme.Red : UITheme.Green;
                return;
            }
            ItemDockMeta.color = UITheme.InkSoft;
            if (custom != null)
            {
                ItemDockMeta.text = custom + (special ? " · " + Loc.T("Special") : string.Empty);
                return;
            }
            if (entry == null)
            {
                ItemDockMeta.text = string.Empty;
                return;
            }
            var meta = ItemCategoryLabel(entry.Category.ToString());
            if (special) meta += " · " + Loc.T("Special");
            ItemDockMeta.text = meta;
        }

        private static void SetItemStatus(string text, bool error)
        {
            ItemStatus = text ?? string.Empty;
            ItemStatusError = error;
            ItemStatusUntil = Time.unscaledTime + 5f;
            UpdateItemDockMeta(GetSelectedItemEntry(), ItemCustomId, ItemCustomId != null || (ItemSelectedEntry != null && ItemSelectedEntry.Special));
        }

        private static void SpawnSelectedItem()
        {
            var target = ItemTargets.Find(x => x.Key == SelectedGrantSteamId);
            var player = ResolveItemTargetPlayer(target);
            if (player == null)
            {
                SetItemStatus(Loc.T("Target player is not online"), true);
                UpdateItemDock();
                return;
            }
            var id = GetSelectedItemId();
            if (string.IsNullOrEmpty(id))
            {
                SetItemStatus(Loc.T("No item selected"), true);
                UpdateItemDock();
                return;
            }
            var entry = GetSelectedItemEntry();
            var special = ItemCustomId != null || (entry != null && entry.Special);
            if (special && !ItemSpecialArmed)
            {
                ItemSpecialArmed = true;
                ItemSpecialArmedUntil = Time.unscaledTime + 3f;
                SetItemStatus(Loc.T("Special item: click again to confirm"), false);
                UpdateItemDock();
                return;
            }
            ItemSpecialArmed = false;

            var result = SpawnItemsUnrestricted(player, id, ItemQuantity, ItemDestination);
            SetItemStatus(result.Text, !result.Ok);
            LogEvent("item-granted", "steam=" + player.SteamId + " item=" + id + " requested=" + ItemQuantity +
                " destination=" + (ItemDestination == 0 ? "backpack" : "ground") + " ok=" + result.Ok);
            UpdateItemDock();
        }

        private static Il2CppMayday.PlayerNet ResolveItemTargetPlayer(GrantTarget target)
        {
            if (target == null) return null;
            if (target.Key == "host")
            {
                var local = Il2CppMirror.NetworkServer.localConnection;
                if (local != null && local.identity != null) return local.identity.GetComponent<Il2CppMayday.PlayerNet>();
                var all = Il2CppMayday.PlayerNet.All;
                if (all != null)
                    for (var i = 0; i < all.Count; i++)
                        if (all[i] != null && all[i].isLocalPlayer) return all[i];
                return null;
            }
            if (ulong.TryParse(target.Key, out var steamId)) return GetLivePlayer(steamId);
            return target.Player;
        }

        private static (string Text, bool Ok) SpawnItemsUnrestricted(Il2CppMayday.PlayerNet player, string itemId, int quantity, int destination)
        {
            if (!Il2CppMirror.NetworkServer.activeHost) return (Loc.T("Only the host can grant items"), false);
            if (player == null || quantity < 1 || quantity > 99) return (Loc.T("Invalid item grant request"), false);
            if (player.Inventory == null || player.ViewTransform == null) return (Loc.T("Target player is not online"), false);

            var granted = 0;
            var world = 0;
            var failed = 0;
            var free = destination == 0 ? Math.Max(0, player.Inventory.FreeSlotCount()) : 0;
            var inventoryCount = Math.Min(quantity, free);
            for (var i = 0; i < inventoryCount; i++)
            {
                try
                {
                    var item = player.Inventory.ServerGive(itemId, null);
                    if (item == null) failed++;
                    else granted++;
                }
                catch (Exception e)
                {
                    failed++;
                    LogEvent("item-grant-inventory-error", "item=" + itemId + " error=" + e.Message);
                }
            }

            var groundCount = quantity - granted - failed;
            var anchor = player.ViewTransform;
            var forward = anchor.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = player.transform.forward;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            for (var i = 0; i < groundCount; i++)
            {
                try
                {
                    var offset = forward * (1.2f + (i % 4) * 0.35f) + right * ((i / 4) * 0.4f - 0.6f);
                    var position = anchor.position + offset;
                    position.y = Mathf.Max(position.y, player.transform.position.y + 0.15f);
                    var rotation = Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);
                    var managed = new Action<Il2CppMayday.Item>(item =>
                    {
                        if (item == null) return;
                        item.NetworkinWorld = true;
                        item.NetworkisUsing = false;
                        item.Networkattach = default(Il2CppMayday.AttachState);
                        item.NetworksyncPos = position;
                        item.NetworksyncRot = rotation;
                        item.ServerTeleport(position, rotation);
                    });
                    var init = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Il2CppMayday.Item>>(managed);
                    var spawned = Il2CppMayday.Item.ServerSpawn(itemId, position, rotation, init, true);
                    if (spawned == null) failed++;
                    else world++;
                }
                catch (Exception e)
                {
                    failed++;
                    LogEvent("item-grant-world-error", "item=" + itemId + " error=" + e.Message);
                }
            }

            if (failed == 0)
                return (Loc.T("Granted item result: inventory {0}, ground {1}", granted, world), true);
            return (Loc.T("Granted item partial result: inventory {0}, ground {1}, failed {2}", granted, world, failed), false);
        }

        private static void RefreshItemUi()
        {
            if (ItemDeliveryCopyUntil > 0f && Time.unscaledTime >= ItemDeliveryCopyUntil)
                ResetDeliveryCopyHint();
            if (ItemSearchFocusClearAt > 0f && Time.unscaledTime >= ItemSearchFocusClearAt)
            {
                ItemSearchFocusClearAt = 0f;
                if (ItemSearchInput != null) ItemSearchInput.DeactivateInputField();
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
            if (!GuiVisible || GuiTab != ItemsTabIndex) return;
            EnsureDeliverySelectorStyles();
            if (ItemSearchDirty && Time.unscaledTime >= ItemSearchDue)
            {
                ItemSearchDirty = false;
                    RefreshItemGrid();
            }
            if (ItemSpecialArmed && Time.unscaledTime > ItemSpecialArmedUntil)
            {
                ItemSpecialArmed = false;
                UpdateItemDock();
            }
            if (ItemStatus.Length > 0 && Time.unscaledTime > ItemStatusUntil)
            {
                ItemStatus = string.Empty;
                UpdateItemDock();
            }
        }

        private static void ResetItemUi()
        {
            DestroyItemDock();
            ItemGridRoot = null;
            ItemSearchRow = null;
            ItemIdRow = null;
            ItemSearchInput = null;
            ItemIdInput = null;
            ItemGridScroll = null;
            ItemQuantityMinusButton = null;
            ItemQuantityPlusButton = null;
            ItemChipButtons.Clear();
            ItemChipValues.Clear();
            ItemTileViews.Clear();
            ItemTileHoveredId = null;
            ItemTileEventSystem = null;
            ItemTilePointer = null;
            ItemTileRaycastHits = null;
            ItemHoverButtons.Clear();
            ItemPointerButton = null;
            ItemPointerSelector = null;
            ItemStatus = string.Empty;
            ItemSearchDirty = false;
            ItemSpecialArmed = false;
            ItemDeliveryCopyHint = null;
            ItemDeliveryCopyUntil = 0f;
            ItemDeliveryTargetButton = null;
            ItemDeliveryTargetLabel = null;
            ItemDeliveryDestSelector = null;
        }

        private static void EnsureItemEntries()
        {
            if (ItemCatalogBuilt && ItemEntries.Count > 0) return;
            ItemEntries.Clear();
            try
            {
                var all = Il2CppMayday.ItemCatalog.all;
                if (all == null || all.Count == 0)
                {
                    Il2CppMayday.ItemCatalog.Build();
                    all = Il2CppMayday.ItemCatalog.all;
                }
                ItemCatalogBuilt = true;
                if (all == null) return;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < all.Count; i++)
                {
                    var info = all[i];
                    if (info == null || string.IsNullOrWhiteSpace(info.id)) continue;
                    if (!seen.Add(info.id)) continue;
                    var entry = new ItemEntry
                    {
                        Id = info.id,
                        NameKey = info.NameKey,
                        Name = info.id,
                        Category = info.category,
                        Implemented = SafeItemImplemented(info.id),
                        Special = IsSpecialItem(info)
                    };
                    ItemEntries.Add(entry);
                }
                RefreshItemNames(Loc.LanguageCodes[Loc.CurrentIndex]);
                ItemEntries.Sort((a, b) =>
                {
                    var byCategory = ItemCategoryRank(a.Category).CompareTo(ItemCategoryRank(b.Category));
                    return byCategory != 0 ? byCategory : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });
                LogEvent("item-catalog", "total=" + all.Count + " unique=" + ItemEntries.Count);
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] 物品目录构建失败: " + e.Message);
            }
        }

        private static void RefreshItemNames(string panelLanguage)
        {
            var nativeCode = panelLanguage switch
            {
                "zh-CN" => "schinese", "en" => "en", "fr" => "french", "de" => "german",
                "es" => "spanish", "pt-BR" => "brazilian", "tr" => "tr", "ru" => "russian",
                "ja" => "japanese", "ko" => "koreana", "pl" => "polish", _ => "en"
            };
            foreach (var entry in ItemEntries)
            {
                var name = ItemNameLocalization.Get(entry.NameKey, entry.Id, panelLanguage);
                entry.Name = string.IsNullOrWhiteSpace(name) ? entry.Id : name;
            }
        }

        private static int ItemCategoryRank(Il2CppMayday.ItemCategory category)
        {
            switch (category)
            {
                case Il2CppMayday.ItemCategory.Cabin: return 0;
                case Il2CppMayday.ItemCategory.Galley: return 1;
                case Il2CppMayday.ItemCategory.Cleaning: return 2;
                case Il2CppMayday.ItemCategory.Maintenance: return 3;
                case Il2CppMayday.ItemCategory.Luggage: return 4;
                case Il2CppMayday.ItemCategory.Crate: return 5;
                default: return 6;
            }
        }

        private static bool SafeItemImplemented(string id)
        {
            try { return Il2CppMayday.ItemCatalog.Implemented(id); }
            catch { return false; }
        }

        private static bool IsSpecialItem(Il2CppMayday.ItemInfo info)
        {
            if (info == null) return true;
            var id = (info.id ?? string.Empty).ToLowerInvariant();
            var keys = new[]
            {
                "bomb", "hijack", "defib", "vest", "seat_kit", "sticky", "pax", "carry"
            };
            for (var i = 0; i < keys.Length; i++)
                if (id.Contains(keys[i])) return true;
            return false;
        }

        private static Sprite GetItemIcon(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try { return Il2CppMayday.UI.ItemIcons.Get(id); }
            catch { return null; }
        }

        private static string GetItemInitials(ItemEntry entry)
        {
            try
            {
                var initials = Il2CppMayday.UI.ItemIcons.Initials(entry.Id);
                if (!string.IsNullOrWhiteSpace(initials)) return initials;
            }
            catch { }
            return entry.Name.Length > 0 ? entry.Name.Substring(0, 1) : "?";
        }

        private static bool IsFavorite(string id)
        {
            return !string.IsNullOrEmpty(id) && ItemFavorites.Contains(id);
        }

        private static void ToggleFavorite(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!ItemFavorites.Add(id)) ItemFavorites.Remove(id);
            if (ItemFavoritesConfig != null)
            {
                ItemFavoritesConfig.Value = string.Join(",", ItemFavorites);
                Settings.Save();
            }
        }

        private static void ToggleSelectedFavorite()
        {
            var entry = GetSelectedItemEntry();
            if (entry == null) return;
            ToggleFavorite(entry.Id);
            if (ItemFavoritesPage)
            {
                if (!HasCatalogFavorites())
                {
                    RebuildContent();
                    return;
                }
                ItemSelectedId = string.Empty;
                ItemSelectedEntry = null;
                ItemCustomId = null;
                RefreshItemGrid();
                UpdateItemDock();
                return;
            }
            UpdateItemDock();
            UpdateItemTileSelection();
        }

        private static string ItemCategoryLabel(string category)
        {
            if (string.IsNullOrEmpty(category)) return Loc.T("All");
            return Loc.T(category);
        }

    }
}
