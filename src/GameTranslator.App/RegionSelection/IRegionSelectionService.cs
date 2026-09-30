using System.Windows;
using GameTranslator.Core;

namespace GameTranslator.App.RegionSelection;

public interface IRegionSelectionService
{
    ScreenRegion? SelectRegion(Window owner);
}
