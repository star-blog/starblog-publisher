# WeChat Theme Picker Redesign

## Goal

Replace the flat `分类 · 名称` ComboBox (33 rows, hard to scan) with a reusable **grouped dropdown** that:

- Shows category headers in the dropdown
- Shows color swatches + theme name per row
- Keeps a compact closed state (swatches + name only)
- Is shared by the WeChat inspector and Settings “默认排版主题”

Chosen UX: **分组下拉** (not flyout search, not two-step category→theme).

## Why not native ComboBox grouping

Avalonia `ItemsControl` / `ComboBox` has **no WPF-style `GroupStyle`**. Docs recommend flattening groups into one list with header items and dual `DataTemplate`s. That is the approach here.

## UX design

### Closed (selected) state

```
[■ ■ ■] 报纸
```

- Three small color chips: `PrimaryColor`, `AccentColor`, `BackgroundColor`
- Theme `Name` only (no `分类 ·` prefix)

### Open dropdown

```
卡片系列                 ← non-selectable header
  [■ ■ ■] 暖光卡片
  [■ ■ ■] 静谧卡片
  [■ ■ ■] 清新卡片
深度长文
  [■ ■ ■] 报纸
  ...
```

- Headers: secondary text, semi-bold, slightly tighter padding; **not selectable**
- Theme rows: swatches + `Name`; optional one-line muted description only in dropdown if space allows (default: name only to keep density)
- Keep `MaxDropDownHeight ≈ 420`
- Description under the control on the WeChat page stays as-is (`SelectedTheme.Description`); Settings omits it

### Category order

Reuse catalog metadata already on `WeChatTheme.Category` / `WeChatThemeCatalog` display order:

1. 深度长文 (newspaper first overall — keep current default-first ordering within groups as catalog `DisplayOrder`)
2. 卡片系列
3. 科技产品
4. 文艺随笔
5. 活力动态
6. 模板布局

Implementation detail: build the flat list by walking `WeChatThemeCatalog.All` in display order and inserting a header whenever `Category` changes. That preserves “newspaper then warm-card…” for the first selectable themes and keeps the desktop test’s “index 1 → warm-card” behavior if the selector still indexes **selectable** items carefully — prefer selecting by **id**, not index, in tests after the change.

## Component: `WeChatThemePicker`

Place under existing shared controls folder (same as `CategoryPicker`):

| File | Role |
|------|------|
| `StarBlogPublisher/Views/Controls/WeChatThemePicker.axaml` | UI |
| `StarBlogPublisher/Views/Controls/WeChatThemePicker.axaml.cs` | Styled properties + header selection guard |

### Public API (StyledProperties)

| Property | Type | Purpose |
|----------|------|---------|
| `SelectedTheme` | `WeChatTheme?` | Object binding (WeChat page) |
| `SelectedThemeId` | `string?` | Id binding (Settings `WeChatDefaultTheme`) |
| `Themes` | `IList<WeChatTheme>?` | Optional override; default `WeChatThemeCatalog.All` |

Two-way sync:

- Changing either `SelectedTheme` or `SelectedThemeId` updates the other via `WeChatThemeCatalog.Resolve` / theme `Id`
- Inner ComboBox selects the matching `ThemeOption` row

### Internal list model

```csharp
// lightweight, GUI-only
public abstract record WeChatThemeListItem;
public sealed record WeChatThemeGroupHeader(string Title) : WeChatThemeListItem;
public sealed record WeChatThemeOption(WeChatTheme Theme) : WeChatThemeListItem;
```

Build once when `Themes` is applied (catalog is static).

### XAML sketch

```xml
<UserControl ... x:Class="...WeChatThemePicker">
  <ComboBox x:Name="ThemeCombo"
            ItemsSource="{Binding Items, RelativeSource={RelativeSource AncestorType=local:WeChatThemePicker}}"
            HorizontalAlignment="Stretch"
            MaxDropDownHeight="420"
            VerticalContentAlignment="Center">
    <ComboBox.DataTemplates>
      <DataTemplate DataType="local:WeChatThemeGroupHeader">
        <TextBlock Text="{Binding Title}" FontWeight="SemiBold" FontSize="11"
                   Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                   Margin="0,6,0,2" />
      </DataTemplate>
      <DataTemplate DataType="local:WeChatThemeOption">
        <StackPanel Orientation="Horizontal" Spacing="8">
          <!-- three Borders bound to Theme.Primary/Accent/Background -->
          <TextBlock Text="{Binding Theme.Name}" VerticalAlignment="Center" />
        </StackPanel>
      </DataTemplate>
    </ComboBox.DataTemplates>
    <ComboBox.SelectionBoxItemTemplate>
      <DataTemplate DataType="local:WeChatThemeOption">
        <!-- same swatches + Name, slightly tighter -->
      </DataTemplate>
    </ComboBox.SelectionBoxItemTemplate>
  </ComboBox>
</UserControl>
```

Use Avalonia’s `SelectionBoxItemTemplate` so the closed box never renders a group header template.

### Header non-selection

On `SelectionChanged` / property change:

1. If new item is `WeChatThemeGroupHeader`, restore previous `WeChatThemeOption`
2. Optionally style header `ComboBoxItem` with `IsEnabled="False"` via `ItemContainerTheme` / style keyed off data type if practical; the guard above is the reliable fallback

Also skip keyboard/pointer selection of headers the same way.

### Color chips

Small `Border` (e.g. 12×12, `CornerRadius="2"`, thin stroke with `CardStrokeColorDefaultBrush`) so light backgrounds stay visible. Bind `Background` with a simple `StringToBrushConverter` if needed, or set brushes in a tiny converter/`IValueConverter` already common in the project — add `HexToBrushConverter` under `Converters/` only if no existing helper.

## Call sites

### WeChat page

Replace the theme `ComboBox` with:

```xml
<controls:WeChatThemePicker SelectedTheme="{Binding SelectedTheme}" />
```

Keep the existing description `TextBlock` and hint below.

`WeChatViewModel.Themes` can remain (still used as catalog source if desired) or the picker can default to the catalog and the VM can drop exposing `Themes` for the ComboBox only — prefer keeping `Themes` on the VM unused by XAML only if something else needs it; otherwise stop binding `ItemsSource` from the page and let the control default to the catalog (simpler Settings reuse).

### Settings page

```xml
<controls:WeChatThemePicker x:Name="WeChatThemeSelector"
                            SelectedThemeId="{Binding WeChatDefaultTheme}" />
```

Drop `ItemsSource` / `SelectedValue` / `DisplayMemberBinding` on the raw ComboBox. Settings can keep or remove `WeChatThemes` property (remove if unused).

## Tests

| Test | Change |
|------|--------|
| `WorkspaceScenarios.SettingsLayout` | Find `WeChatThemePicker` (or named control), set `SelectedThemeId = "warm-card"` (or select second **theme option**, not raw `SelectedIndex` on mixed header list). Assert `vm.WeChatDefaultTheme == "warm-card"`. |
| `WeChatViewModelTests` | Still “Themes not empty / SelectedTheme not null”; optionally assert picker-independent. |
| New unit/GUI helper (optional) | `WeChatThemePicker` builds N headers + 33 options; selecting a header does not change `SelectedThemeId`. |

## Implementation steps

1. Add list-item types + `HexToBrushConverter` (if needed).
2. Implement `WeChatThemePicker` UserControl (templates, sync properties, header guard).
3. Wire WeChat page + Settings page; remove duplicate ComboBox markup.
4. Update desktop settings scenario to select by theme id.
5. Manual smoke: open WeChat inspector + Settings WeChat tab, scroll groups, pick themes, confirm description updates and settings draft dirty-state still tracks `WeChatDefaultTheme`.

## Out of scope

- Flyout search panel / CategoryPicker-style overlay
- Two-step category then theme
- Live HTML mini-preview thumbnails in the dropdown
- Changing theme JSON or catalog categories

## Success criteria

- Dropdown is scannable by wechat-pub category
- Closed control shows swatches + short name (not `深度长文 · 报纸`)
- One control used in both places
- Existing theme id persistence (`WeChatDefaultTheme`, `tech`→`github`) unchanged
- Unit tests green; desktop settings theme assertion updated and passing
