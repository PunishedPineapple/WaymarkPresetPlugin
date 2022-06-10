using CheapLoc;

namespace WaymarkPresetPlugin
{
	internal enum ZoneSortType : int
	{
		Basic,
		Alphabetical,
		Custom
	}

	internal static class ZoneSortTypeExtensions
	{
		internal static string GetTranslatedName( this ZoneSortType sortType )
		{
			return sortType switch
			{
				ZoneSortType.Basic => Loc.Localize( "Terminology: Zone Sort Type - Basic", "Sort by ID" ),
				ZoneSortType.Alphabetical => Loc.Localize( "Terminology: Zone Sort Type - Alphabetical", "Sort Alphabetically" ),
				ZoneSortType.Custom => Loc.Localize( "Terminology: Zone Sort Type - Custom", "Custom Sort Order" ),
				_ => "You should never see this!",
			};
		}
	}
}
