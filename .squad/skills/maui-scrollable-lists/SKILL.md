---
name: "maui-scrollable-lists"
description: "How to render multiple list sections inside a ScrollView in MAUI without nested-scroll conflicts"
domain: "ui, maui, layout"
confidence: "high"
source: "earned"
---

## Context
MAUI's `CollectionView` manages its own internal scroll. Placing a `CollectionView` inside a `ScrollView` creates a nested-scroll conflict where items may not render, measure incorrectly, or be inaccessible. This becomes a problem when a view needs two or more list sections (e.g., a folder queue and a file queue) in the same scrollable page.

## Patterns

**Use `BindableLayout` on `VerticalStackLayout` inside a `ScrollView` for multi-section lists:**

```xml
<ScrollView>
    <VerticalStackLayout Spacing="16">
        <!-- Section A -->
        <VerticalStackLayout BindableLayout.ItemsSource="{Binding Items}">
            <BindableLayout.ItemTemplate>
                <DataTemplate x:DataType="entities:MyEntity">
                    <!-- item UI -->
                </DataTemplate>
            </BindableLayout.ItemTemplate>
        </VerticalStackLayout>
    </VerticalStackLayout>
</ScrollView>
```

**Cross-assembly entity xmlns:** When `x:DataType` references an entity in a different project assembly, include the `assembly=` qualifier:

```xml
xmlns:entities="clr-namespace:DamYou.Data.Entities;assembly=DamYou.Data"
```

**Empty state visibility:** Use `IsVisible="{Binding IsEmpty}"` on a VerticalStackLayout alongside `IsVisible="{Binding HasItems}"` on list sections rather than `CollectionView.EmptyView`, since EmptyView only works on CollectionView.

## Examples

`src/DamYou/Views/WorkQueueView.xaml` — Two BindableLayout sections (QueuedFolders and QueuedFiles) inside a single ScrollView.

## Anti-Patterns

- **Do not** use `CollectionView` inside a `ScrollView` — this causes nested-scroll conflicts and broken measurements.
- **Do not** omit `assembly=` in the xmlns when the entity type is in a referenced project assembly; the compiled binding will fail at build time.
- `BindableLayout` is appropriate for moderate list sizes (< ~200 items). For large virtualized lists, restructure the page so `CollectionView` is the outer scroll container.
