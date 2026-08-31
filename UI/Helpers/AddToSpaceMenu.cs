using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiniT.Core.Models;
using MiniT.UI.ViewModels;

namespace MiniT.UI.Helpers;

/// <summary>
/// Builds and shows the "Add to space" flyout for a single tool: a checkable
/// list of every real (non-system) space, toggled on click. This is the one
/// place that logic lives, so it behaves identically wherever a tool can be
/// found — a card inside a Space (including the built-in "All Tools" space,
/// which exists specifically as a browse-everything surface to add from), or
/// a row in the Ctrl+K search results.
/// </summary>
public static class AddToSpaceMenu
{
    public static void Show(MainViewModel vm, ToolDefinition tool, FrameworkElement anchor,
        Windows.Foundation.Point? position = null, Action? onChanged = null)
    {
        var menu = new MenuFlyout();
        var spaces = vm.RealSpaces.ToList();

        if (spaces.Count == 0)
        {
            menu.Items.Add(new MenuFlyoutItem
            {
                Text = "No spaces yet — create one first",
                IsEnabled = false
            });
        }
        else
        {
            foreach (var space in spaces)
            {
                var item = new ToggleMenuFlyoutItem
                {
                    Text = space.Name,
                    IsChecked = vm.IsToolInSpace(space, tool)
                };
                // ToggleMenuFlyoutItem flips IsChecked before Click fires, so it
                // already reflects the new desired state here.
                item.Click += (_, _) =>
                {
                    if (item.IsChecked) vm.AddToolToSpace(tool, space);
                    else vm.RemoveToolFromSpace(tool, space);
                    onChanged?.Invoke();
                };
                menu.Items.Add(item);
            }
        }

        if (position.HasValue) menu.ShowAt(anchor, position.Value);
        else menu.ShowAt(anchor);
    }
}
