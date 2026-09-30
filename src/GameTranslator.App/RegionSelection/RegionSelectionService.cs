using System.Windows;
using GameTranslator.Core;

namespace GameTranslator.App.RegionSelection;

public sealed class RegionSelectionService : IRegionSelectionService
{
    public ScreenRegion? SelectRegion(Window owner)
    {
        var selector = new RegionSelectorWindow(new PhysicalScreenCoordinateMapper())
        {
            Owner = owner
        };

        return selector.ShowDialog() == true ? selector.SelectedRegion : null;
    }
}
