using System;
using System.Collections;

/* Classe rapatriée du package Xamarin.Forms.CarouselView v2.3.0-pre2
 * Tout en faisant la mise à jour, on évite d'utiliser le CarouselView de
 * Xamarin.Forms v3.6 car il est basé sur CollectionView qui est encore experimental.
 */

namespace CarouselViewForms
{
    public delegate void CollectionSynchronizationCallback(IEnumerable collection, object context, Action accessMethod, bool writeAccess);

    public class CollectionSynchronizationContext
    {
        public CollectionSynchronizationCallback Callback { get; private set; }

        public object Context
        {
            get { return ContextReference != null ? ContextReference.Target : null; }
        }

        public WeakReference ContextReference { get; }

        internal CollectionSynchronizationContext(object context, CollectionSynchronizationCallback callback)
        {
            ContextReference = new WeakReference(context);
            Callback = callback;
        }
    }
}