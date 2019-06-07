using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Xamarin.Forms;

/* Classe rapatriée du package Xamarin.Forms.CarouselView v2.3.0-pre2
 * Tout en faisant la mise à jour, on évite d'utiliser le CarouselView de
 * Xamarin.Forms v3.6 car il est basé sur CollectionView qui est encore experimental.
 */

namespace CarouselViewForms
{
    public class CarouselView : ItemView, ICarouselViewController
    {
        private static object s_defaultItem = new object();
        private static object s_defaultView = new Label();
        private readonly DataTemplate _defaultDataTemplate;
        private CarouselViewItemSource _itemsSource;
        private object _lastItem;
        private int _lastPosition;
        private bool _ignorePositionUpdate;
        public static readonly BindableProperty PositionProperty =
            BindableProperty.Create(
                propertyName: nameof(Position),
                returnType: typeof(int),
                declaringType: typeof(CarouselView),
                defaultValue: 0,
                defaultBindingMode: BindingMode.TwoWay,
                validateValue: (b, o) => ((CarouselView)b).OnValidatePosition((int)o),
                propertyChanged: (b, o, n) => ((CarouselView)b).OnPositionChanged()
            );

        public static readonly BindableProperty ItemProperty =
            BindableProperty.Create(
                propertyName: nameof(Item),
                returnType: typeof(object),
                declaringType: typeof(CarouselView),
                defaultValue: null,
                defaultBindingMode: BindingMode.TwoWay,
                coerceValue: (b, o) => ((CarouselView)b).OnCoerceItem(o)
            );

#pragma warning disable 414
#if MOBILE
		static Type s_type = typeof(CarouselViewRenderer); // Force load of renderer assembly
#endif
#pragma warning restore 414

        event NotifyCollectionChangedEventHandler ICarouselViewController.CollectionChanged
        {
            add
            {
                CollectionChanged += value;
            }

            remove
            {
                CollectionChanged -= value; ;
            }
        }

        public event EventHandler<SelectedItemChangedEventArgs> ItemSelected;

        public event EventHandler<SelectedPositionChangedEventArgs> PositionSelected;

        private int InternalPosition
        {
            get { return Position; }
            set { ((IElementController)this).SetValueFromRenderer(PositionProperty, value); }
        }

        private object InternalItem
        {
            get { return Item; }
            set { ((IElementController)this).SetValueFromRenderer(ItemProperty, value); }
        }

        private View DefaultView
        {
            get; set;
        }

        private ICarouselViewController Controller => this;

        int ICarouselViewController.Position
        {
            get { return InternalPosition; }
            set { InternalPosition = value; }
        }

        object ICarouselViewController.Item
        {
            get { return InternalItem; }
            set { InternalItem = value; }
        }

        public int Position
        {
            get { return (int)GetValue(PositionProperty); }
            set { SetValue(PositionProperty, value); }
        }

        public object Item
        {
            get { return GetValue(ItemProperty); }
            internal set { SetValue(ItemProperty, value); }
        }

        bool ICarouselViewController.IgnorePositionUpdates => _itemsSource.IsNull;

        public CarouselView()
        {
            _lastPosition = 0;
            _lastItem = null;
            _defaultDataTemplate = new DataTemplate(() => DefaultView ?? s_defaultView);

            VerticalOptions = LayoutOptions.FillAndExpand;
            HorizontalOptions = LayoutOptions.FillAndExpand;
        }

        private void SendChangedEvents()
        {
            if (_lastPosition != Position)
                PositionSelected?.Invoke(this, new SelectedPositionChangedEventArgs(Position));
            _lastPosition = Position;

            if (!Equals(_lastItem, Item))
                ItemSelected?.Invoke(this, new SelectedItemChangedEventArgs(Item));
            _lastItem = Item;
        }

        private object OnCoerceItem(object item) => item == s_defaultItem ? null : item;

        private void OnPositionChanged()
        {
            // if renderer is ignoring position updates then manually update position
            if (Controller.IgnorePositionUpdates)
            {
                if (!_ignorePositionUpdate)
                    SendChangedEvents();
            }
        }

        private bool OnValidatePosition(int value)
        {
            if (value < 0)
                return false;

            if (_itemsSource.IsNull)
                return true;

            if (_itemsSource.IsEmpty)
                return value == 0;

            return value < Controller.Count;
        }

        // vNext feature
        protected override SizeRequest OnMeasure(double widthConstraint, double heightConstraint)
        {
            var minimumSize = new Size(40, 40);
            return new SizeRequest(minimumSize, minimumSize);
        }

        protected override DataTemplate GetDataTemplate(object item)
        {
            if (item == s_defaultItem)
                return _defaultDataTemplate;

            return base.GetDataTemplate(item);
        }

        protected override IReadOnlyList<object> OnInitializeItemSource()
        {
            return _itemsSource = new CarouselViewItemSource();
        }

        protected override IReadOnlyList<object> OnItemsSourceChanging(
            IReadOnlyList<object> itemsSource,
            ref NotifyCollectionChangedEventHandler collectionChanged)
        {
            // when ItemsSource is null any initial position can be selected
            if (itemsSource != null)
            {
                // tell renderer to ignore Position updates while we whack positions
                _itemsSource.ItemsSource = null;

                // when ItemsSource is empty position can and must be zero
                if (itemsSource.Count == 0)
                {
                    InternalItem = null;
                    InternalPosition = 0;
                }

                // we're short on items
                else if (itemsSource.Count <= Position)
                {
                    _ignorePositionUpdate = true;
                    InternalPosition = itemsSource.Count - 1;
                    _ignorePositionUpdate = false;
                }
            }

            // intercept calls to ItemSource
            _itemsSource.ItemsSource = itemsSource;

            if (itemsSource == null)
                return _itemsSource;

            // intercept calls from CollectionChanged
            var baseCollectionChanged = collectionChanged;
            collectionChanged = (s, e) =>
            {
                // when user itemsSource is empty provide a default view
                var removeLast = _itemsSource.ItemsSource.Count == 0 && e.Action == NotifyCollectionChangedAction.Remove;

                // when user itemsSource adds first item then reset to clear default view
                var addFirst = _itemsSource.ItemsSource.Count == 1 && e.Action == NotifyCollectionChangedAction.Add;

                if (addFirst || removeLast)
                {
                    e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);

                    // not ideal; happens before default item appears or disappears
                    InternalItem = Controller.GetItem(Position);
                    SendChangedEvents();
                }

                baseCollectionChanged(s, e);
            };

            return _itemsSource;
        }

        internal override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            InternalItem = Controller.GetItem(Position);

            // notify app of position changes
            Controller.SendSelectedPositionChanged(Position);

            base.OnItemsSourceChanged(oldValue, newValue);
        }

        void ICarouselViewController.SendSelectedItemChanged(object item)
        {
            InternalItem = item;
            SendChangedEvents();
        }

        void ICarouselViewController.SendSelectedPositionChanged(int position)
        {
            InternalPosition = position;
            InternalItem = Controller.GetItem(position);
            SendChangedEvents();
        }

        private sealed class CarouselViewItemSource : IReadOnlyList<object>
        {
            private IReadOnlyList<object> _itemsSource;

            internal IReadOnlyList<object> ItemsSource
            {
                get
                {
                    return _itemsSource;
                }
                set
                {
                    _itemsSource = value;
                }
            }

            internal bool IsNull => _itemsSource == null;

            internal bool IsEmpty => !IsNull && _itemsSource.Count == 0;

            internal bool IsNullOrEmpty => IsNull || IsEmpty;

            public int Count
            {
                get
                {
                    // allow any initial value
                    // renderers see infinite list items all of which are s_defaultView
                    if (IsNull)
                        return int.MaxValue;

                    // Position will have been set to 0
                    // renderers see a list of a single item which is s_defaultView
                    if (IsEmpty)
                        return 1;

                    return _itemsSource.Count;
                }
            }

            public object this[int index]
            {
                get
                {
                    if (IsNullOrEmpty)
                        return s_defaultItem;

                    return _itemsSource[index];
                }
            }

            public IEnumerator<object> GetEnumerator()
            {
                // ItemsView never actually uses GetEnumerator
                throw new NotSupportedException();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}