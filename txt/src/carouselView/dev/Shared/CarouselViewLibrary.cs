/* Classe rapatriée du package Xamarin.Forms.CarouselView v2.3.0-pre2
 * Tout en faisant la mise à jour, on évite d'utiliser le CarouselView de
 * Xamarin.Forms v3.6 car il est basé sur CollectionView qui est encore experimental.
 */

namespace CarouselViewForms
{
#if SHARED
	public static partial class CarouselViewLibrary
	{
		public static void Init() => PlatformInit();
		static partial void PlatformInit();
	}
#endif
}