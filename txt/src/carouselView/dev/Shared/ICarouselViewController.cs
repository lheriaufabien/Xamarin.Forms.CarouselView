using System.Collections.Specialized;
using Xamarin.Forms;

/* Classe rapatriée du package Xamarin.Forms.CarouselView v2.3.0-pre2
 * Tout en faisant la mise à jour, on évite d'utiliser le CarouselView de
 * Xamarin.Forms v3.6 car il est basé sur CollectionView qui est encore experimental.
 */

namespace CarouselViewForms
{
    public interface ICarouselViewController : IItemViewController
    {
        event NotifyCollectionChangedEventHandler CollectionChanged;

        bool IgnorePositionUpdates
        {
            get;
        }

        int Position { get; set; }

        object Item { get; set; }

        void SendSelectedItemChanged(object item);

        void SendSelectedPositionChanged(int position);
    }
}