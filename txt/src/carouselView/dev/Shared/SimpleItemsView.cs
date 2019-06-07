using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using Xamarin.Forms;

/* Classe rapatriée du package Xamarin.Forms.CarouselView v2.3.0-pre2
 * Tout en faisant la mise à jour, on évite d'utiliser le CarouselView de
 * Xamarin.Forms v3.6 car il est basé sur CollectionView qui est encore experimental.
 */

namespace CarouselViewForms
{
    public abstract class ItemView : View, IItemViewController
    {
        private ItemsSourceProxy _itemSourceProxy;
        private NotifyCollectionChangedEventHandler _onCollectionChanged;
        private NotifyCollectionChangedEventHandler _onCollectionChangedProxy;
        public static readonly BindableProperty ItemsSourceProperty =
                                    BindableProperty.Create(
                propertyName: "ItemsSource",
                returnType: typeof(IEnumerable),
                declaringType: typeof(ItemView),
                propertyChanging: (b, o, n) => ((ItemView)b).OnItemsSourceChanging((IEnumerable)o, (IEnumerable)n)
            );

        public static readonly BindableProperty ItemTemplateProperty =
            BindableProperty.Create(
                propertyName: "ItemTemplate",
                returnType: typeof(DataTemplate),
                declaringType: typeof(ItemView)
            );

        internal event NotifyCollectionChangedEventHandler CollectionChanged;

        private IItemViewController Controller => this;

        public IEnumerable ItemsSource
        {
            get
            {
                return (IEnumerable)GetValue(ItemsSourceProperty);
            }
            set
            {
                SetValue(ItemsSourceProperty, value);
            }
        }

        public DataTemplate ItemTemplate
        {
            get
            {
                return (DataTemplate)GetValue(ItemTemplateProperty);
            }
            set
            {
                SetValue(ItemTemplateProperty, value);
            }
        }

        int IItemViewController.Count => _itemSourceProxy.Count;

        public ItemView()
        {
            _onCollectionChanged = _onCollectionChangedProxy = OnCollectionChanged;

            _itemSourceProxy = new ItemsSourceProxy(
                itemsSource: Enumerable.Empty<object>(),
                itemsSourceAsList: OnInitializeItemSource(),
                onCollectionChanged: new WeakReference<NotifyCollectionChangedEventHandler>(_onCollectionChangedProxy)
            );
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (CollectionChanged != null)
                CollectionChanged(sender, e);
        }

        protected abstract IReadOnlyList<object> OnInitializeItemSource();

        protected abstract IReadOnlyList<object> OnItemsSourceChanging(
            IReadOnlyList<object> itemSource,
            ref NotifyCollectionChangedEventHandler collectionChanged);

        protected virtual DataTemplate GetDataTemplate(object item) => null;

        internal virtual void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
        }

        internal void OnItemsSourceChanging(IEnumerable oldValue, IEnumerable newValue)
        {
            // wrap up enumerable, IList, IList<T>, and IReadOnlyList<T>
            var itemSourceAsList = newValue?.ToReadOnlyList();

            // allow interception of itemSource
            var onCollectionChanged = _onCollectionChanged;
            itemSourceAsList = OnItemsSourceChanging(itemSourceAsList, ref onCollectionChanged);
            if (itemSourceAsList == null)
                throw new InvalidOperationException(
                    "OnItemsSourceChanging must return non-null itemSource as IReadOnlyList");

            // keep callback alive
            _onCollectionChangedProxy = onCollectionChanged;

            // dispatch CollectionChangedEvent to ItemView without a strong reference to ItemView and
            // synchronize dispatch and element access via CollectionSynchronizationContext protocol
            _itemSourceProxy?.Dispose();
            _itemSourceProxy = new ItemsSourceProxy(
                itemsSource: newValue,
                itemsSourceAsList: itemSourceAsList,
                onCollectionChanged: new WeakReference<NotifyCollectionChangedEventHandler>(_onCollectionChangedProxy)
            );

            OnItemsSourceChanged(oldValue, newValue);
        }

        void IItemViewController.BindView(View view, object item) => view.BindingContext = item;

        object IItemViewController.GetItem(int index) => _itemSourceProxy[index];

        View IItemViewController.CreateView(object type)
        {
            object content = ((DataTemplate)type).CreateContent();
            var view = content as View;
            if (view == null)
                throw new InvalidOperationException($"DataTemplate returned non-view content: '{content}'.");

            view.Parent = this;
            return view;
        }

        object IItemViewController.GetItemType(object item)
        {
            // allow interception of DataTemplate resolution
            var dataTemplate = GetDataTemplate(item);
            if (dataTemplate != null)
                return dataTemplate;

            // resolve DataTemplate (possibly via DataTemplateSelector)
            dataTemplate = ItemTemplate;
            var dataTemplateSelector = dataTemplate as DataTemplateSelector;
            if (dataTemplateSelector != null)
                dataTemplate = dataTemplateSelector.SelectTemplate(item, this);

            if (item == null)
                throw new ArgumentException($"No DataTemplate resolved for item: '{item}'.");

            return dataTemplate;
        }

        private sealed class ItemsSourceProxy : IDisposable
        {
            private readonly object _itemsSource;
            private readonly IReadOnlyList<object> _itemsSourceAsList;
            private readonly WeakReference<NotifyCollectionChangedEventHandler> _onCollectionChanged;

            private CollectionSynchronizationContext SyncContext
            {
                get
                {
                    if (_itemsSource == null)
                        return null;

                    BindingBase.TryGetSynchronizedCollection((IEnumerable)_itemsSource, out CollectionSynchronizationContext syncContext);
                    return syncContext;
                }
            }

            public int Count => _itemsSourceAsList.Count;

            public object this[int index]
            {
                get
                {
                    // Device.IsInvokeRequired will be false

                    if (SyncContext != null)
                    {
                        object value = null;
                        Synchronize(() => value = _itemsSourceAsList[index]);
                        return value;
                    }

                    return _itemsSourceAsList[index];
                }
            }

            internal ItemsSourceProxy(
                                                    object itemsSource,
                IReadOnlyList<object> itemsSourceAsList,
                WeakReference<NotifyCollectionChangedEventHandler> onCollectionChanged)
            {
                _itemsSource = itemsSource;
                _itemsSourceAsList = itemsSourceAsList;
                _onCollectionChanged = onCollectionChanged;

                var dynamicItemSource = itemsSource as INotifyCollectionChanged;
                if (dynamicItemSource == null)
                    return;

                dynamicItemSource.CollectionChanged += SynchronizeOnCollectionChanged;
            }

            private void Synchronize(Action action)
            {
                MethodInfo callbackMethod = SyncContext.GetType().GetRuntimeMethod("Callback", new Type[] { typeof(IEnumerable), typeof(Action) });
                callbackMethod.Invoke(SyncContext, new object[] { _itemsSource, action });
            }

            private void SynchronizeOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                if (SyncContext != null)
                {
                    Synchronize(() => OnCollectionChanged(sender, e));
                    return;
                }

                OnCollectionChanged(sender, e);
            }

            private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                if (Device.IsInvokeRequired)
                {
                    Device.BeginInvokeOnMainThread(() => OnCollectionChanged(sender, e));
                    return;
                }

                NotifyCollectionChangedEventHandler onCollectionChanged;
                if (!_onCollectionChanged.TryGetTarget(out onCollectionChanged))
                {
                    var dynamicItemSource = (INotifyCollectionChanged)_itemsSource;
                    dynamicItemSource.CollectionChanged -= SynchronizeOnCollectionChanged;
                    return;
                }

                onCollectionChanged(sender, e);
            }

            public void Dispose()
            {
                _onCollectionChanged.SetTarget(null);
            }
        }
    }
}