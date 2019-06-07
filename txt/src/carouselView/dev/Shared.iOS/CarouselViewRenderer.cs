using System;
using System.Collections.Generic;
using Xamarin.Forms.Platform.iOS;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

#if __UNIFIED__

using UIKit;
using Foundation;

#else
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
#endif
#if __UNIFIED__

using RectangleF = CoreGraphics.CGRect;
using SizeF = CoreGraphics.CGSize;
using CarouselViewForms;
using Xamarin.Forms;

using CoreGraphics;
using System.Reflection;

#else
using nfloat = System.Single;
using nint = System.Int32;
using nuint = System.UInt32;
#endif

/* Classe rapatriée du package Xamarin.Forms.CarouselView v2.3.0-pre2
 * Tout en faisant la mise à jour, on évite d'utiliser le CarouselView de
 * Xamarin.Forms v3.6 car il est basé sur CollectionView qui est encore experimental.
 */

[assembly: ExportRenderer(typeof(CarouselViewForms.CarouselView), typeof(CarouselViewForms.iOS.Renderers.CarouselViewRenderer))]
namespace CarouselViewForms.iOS.Renderers
{
    internal sealed class CarouselViewController : UICollectionViewController
    {
        private readonly Dictionary<object, int> _typeIdByType;

        private CarouselViewRenderer _renderer;

        private int _nextItemTypeId;

        private int? _initialPosition;

        private int _lastPosition;

        internal Action<int> OnPositionChanged;

        private CarouselViewRenderer Renderer => _renderer;

        private ICarouselViewController Controller => Element;

        CarouselViewForms.CarouselView Element => _renderer.Element;

        internal CarouselViewController(
            CarouselViewRenderer renderer)
            : base(new Layout(UICollectionViewScrollDirection.Horizontal))
        {
            _renderer = renderer;
            _typeIdByType = new Dictionary<object, int>();
            _nextItemTypeId = 0;
            _lastPosition = 0;
        }

        [Export("collectionView:layout:sizeForItemAtIndexPath:")]
        private SizeF GetSizeForItem(
            UICollectionView collectionView,
            UICollectionViewLayout layout,
            NSIndexPath indexPath)
        {
            return collectionView.Frame.Size;
        }

        private void DisplayCell()
        {
            if (CollectionView.VisibleCells.Length == 0)
                return;

            // only ever seems to be a single cell visible at a time
            var visibleCell = (Cell)CollectionView.VisibleCells[0];
            var position = visibleCell.Position;
            if (position == _lastPosition)
                return;

            _lastPosition = position;
            OnPositionChanged(position);
        }

        internal void ReloadData(int? initialPosition = null)
        {
            if (initialPosition == null)
                initialPosition = _lastPosition;

            _initialPosition = initialPosition;
            CollectionView.ReloadData();
        }

        internal void ReloadItems(IEnumerable<int> positions)
        {
            var indices = positions.Select(o => NSIndexPath.FromRowSection(o, 0)).ToArray();
            CollectionView.ReloadItems(indices);
        }

        internal void DeleteItems(IEnumerable<int> positions)
        {
            var indices = positions.Select(o => NSIndexPath.FromRowSection(o, 0)).ToArray();
            CollectionView.DeleteItems(indices);
        }

        internal void MoveItem(int oldPosition, int newPosition)
        {
            base.MoveItem(
                CollectionView,
                NSIndexPath.FromRowSection(oldPosition, 0),
                NSIndexPath.FromRowSection(newPosition, 0)
            );
        }

        internal void ScrollToPosition(int position, bool animated = true)
        {
            CollectionView.ScrollToItem(
                indexPath: NSIndexPath.FromRowSection(position, 0),
                scrollPosition: UICollectionViewScrollPosition.CenteredHorizontally,
                animated: animated
            );
        }

        public override void CellDisplayingEnded(
            UICollectionView collectionView,
            UICollectionViewCell cell,
            NSIndexPath indexPath)
        {
            if (_initialPosition != null)
                return;

            DisplayCell();
        }

        public override void WillDisplayCell(
            UICollectionView collectionView,
            UICollectionViewCell cell,
            NSIndexPath indexPath)
        {
            // silently scroll to initial position
            if (_initialPosition != null)
            {
                ScrollToPosition((int)_initialPosition, false);
                _initialPosition = null;
                return;
            }

            DisplayCell();
        }

        public override nint NumberOfSections(UICollectionView collectionView) => 1;

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            CollectionView.PagingEnabled = true;
            CollectionView.BackgroundColor = UIColor.Clear;
            CollectionView.ContentInset = new UIEdgeInsets(0, 0, 0, 0);
        }

        public override nint GetItemsCount(UICollectionView collectionView, nint section)
        {
            var count = Controller.Count;

            // this happens when CarouselView has a null ItemsSource. CarouselView is *trying* to tell iOS
            // that all positions are valid by saying Count is int.MaxValue and then when iOS asks for any position
            // the default view can be returned. Unfortunetly, iOS allocates memory upfront for all positions
            // so will hang trying to allocate int.MaxValue slots.

            // Android works because our bespoke renderer lazily allocates memory so can start at any position;
            // its is more memory efficient that the stock iOS or even Android renderer in this regard. Yea us.
            if (count == int.MaxValue)
                count = _initialPosition + 1 ?? 0;

            return count;
        }

        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var index = indexPath.Row;

            // load initial position then silently scroll to position (see WillDisplayCell)
            if (_initialPosition != null)
            {
                index = (int)_initialPosition;

                // no need to scroll if we're already at the inital position
                if (_initialPosition == _lastPosition)
                    _initialPosition = null;
            }

            var item = Controller.GetItem(index);
            var itemType = Controller.GetItemType(item);

            var itemTypeId = default(int);
            if (!_typeIdByType.TryGetValue(itemType, out itemTypeId))
            {
                _typeIdByType[itemType] = itemTypeId = _nextItemTypeId++;
                CollectionView.RegisterClassForCell(typeof(Cell), itemTypeId.ToString());
            }

            var cell = (Cell)CollectionView.DequeueReusableCell(itemTypeId.ToString(), indexPath);
            cell.Initialize(Element, itemType, item, index);

            return cell;
        }

        private new sealed class Layout : UICollectionViewFlowLayout
        {
            private static readonly nfloat ZeroMinimumInteritemSpacing = 0;
            private static readonly nfloat ZeroMinimumLineSpacing = 0;

            public Layout(UICollectionViewScrollDirection scrollDirection)
            {
                ScrollDirection = scrollDirection;
                MinimumInteritemSpacing = ZeroMinimumInteritemSpacing;
                MinimumLineSpacing = ZeroMinimumLineSpacing;
            }
        }

        private sealed class Cell : UICollectionViewCell
        {
            private IItemViewController _controller;
            private int _position;
            private IVisualElementRenderer _renderer;
            private View _view;

            internal int Position => _position;

            [Export("initWithFrame:")]
            internal Cell(RectangleF frame) : base(frame)
            {
                _position = -1;
            }

            private void Bind(object item, int position)
            {
                _position = position;
                _controller.BindView(_view, item);
            }

            internal void Initialize(IItemViewController controller, object itemType, object item, int position)
            {
                _position = position;

                if (_controller == null)
                {
                    _controller = controller;

                    // create view
                    _view = controller.CreateView(itemType);

                    // bind view
                    Bind(item, _position);

                    // render view
                    _renderer = Platform.CreateRenderer(_view);
                    Platform.SetRenderer(_view, _renderer);

                    // attach view
                    var uiView = _renderer.NativeView;
                    ContentView.AddSubview(uiView);
                }
                else
                    Bind(item, _position);
            }

            public override void LayoutSubviews()
            {
                base.LayoutSubviews();
                _renderer.Element.Layout(new Rectangle(0, 0, ContentView.Frame.Width, ContentView.Frame.Height));
            }
        }
    }

    /// </summary>
    public class CarouselViewRenderer : ViewRenderer<CarouselViewForms.CarouselView, UICollectionView>
    {
        private const int DefaultItemsCount = 1;
        private const int DefaultMinimumDimension = 44;

        // As on Android, ScrollToPostion from 0 to 2 should not raise OnPositionChanged for 1
        // Tracking the _targetPosition allows for skipping events for intermediate positions
        private int? _scrollToTarget;

        private int _position;
        private bool _disposed;
        private CarouselViewController _controller;

        private CGRect? _lastBounds;

        private MethodInfo _controllerReloadDataMethod = null;

        private MethodInfo _controllerScrollToPositionMethod = null;

        private Type _controllerType = null;

        private ICarouselViewController _carouselController => Element;

        /// <summary>
        /// The underlying controller has an internal type, so I have to get it by reflection
        /// </summary>
        protected Type ControllerType
        {
            get
            {
                if (_controllerType == null)
                {
                    _controllerType = Controller?.GetType();
                }
                return _controllerType;
            }
        }

        /// <summary>
        /// The method that reloads the data on the Controller, it is an internal method so I have to get it by reflection
        /// </summary>
        protected MethodInfo ControllerReloadDataMethod
        {
            get
            {
                if (_controllerReloadDataMethod == null)
                {
                    _controllerReloadDataMethod = ControllerType?.GetMethod(nameof(ReloadData), BindingFlags.Instance | BindingFlags.NonPublic);
                }
                return _controllerReloadDataMethod;
            }
        }

        /// <summary>
        /// The method that scrolls to position on the Controller, it is an internal method so I have to get it by reflection
        /// </summary>
        protected MethodInfo ControllerScrollToPositionMethod
        {
            get
            {
                if (_controllerScrollToPositionMethod == null)
                {
                    _controllerScrollToPositionMethod = ControllerType?.GetMethod(nameof(ScrollToPosition), BindingFlags.Instance | BindingFlags.NonPublic);
                }
                return _controllerScrollToPositionMethod;
            }
        }

        /// <summary>
        /// The underlying controller of the view
        /// </summary>
        internal UICollectionViewController Controller
        {
            get
            {
                if (_controller == null)
                {
                    FieldInfo fi = typeof(CarouselViewRenderer).GetField(nameof(_controller), BindingFlags.NonPublic | BindingFlags.Instance);
                    object cvc = fi?.GetValue(this);
                    _controller = (CarouselViewController)cvc;
                }
                return _controller;
            }
        }

        public CarouselViewRenderer()
        {
        }

        private void Initialize()
        {
            // cache hit?
            var carouselView = base.Control;
            if (carouselView != null)
                return;

            _lastBounds = Bounds;
            _controller = new CarouselViewController(
                renderer: this
            );

            // hook up on position changed event
            _controller.OnPositionChanged = OnPositionChange;

            // populate cache
            SetNativeControl(_controller.CollectionView);
        }

        private void OnItemChange(int position)
        {
            var item = _carouselController.GetItem(position);
            _carouselController.SendSelectedItemChanged(item);
        }

        private void OnPositionChange(int position)
        {
            // do not report intermediate positions while scrolling
            if (_scrollToTarget != null)
            {
                if (position != _scrollToTarget)
                    return;
                _scrollToTarget = null;
            }
            else if (position == _position)
            {
                return;
            }

            _position = position;
            _carouselController.Position = position;

            _carouselController.SendSelectedPositionChanged(position);
        }

        private void OnCollectionChanged(object source, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    _controller.ReloadData();

                    if (e.NewStartingIndex <= _position)
                        ShiftPosition(e.NewItems.Count);

                    break;

                case NotifyCollectionChangedAction.Move:
                    for (var i = 0; i < e.NewItems.Count; i++)
                    {
                        _controller.MoveItem(
                            oldPosition: e.OldStartingIndex + i,
                            newPosition: e.NewStartingIndex + i
                        );
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (_carouselController.Count == 0)
                        throw new InvalidOperationException("CarouselView must retain a least one item.");

                    var removedPosition = e.OldStartingIndex;

                    if (removedPosition == _position)
                    {
                        _controller.DeleteItems(
                            Enumerable.Range(e.OldStartingIndex, e.OldItems.Count)
                        );
                        if (_position == _carouselController.Count)
                            OnPositionChange(_position - 1);
                        OnItemChange(_position);
                    }
                    else if (removedPosition > _position)
                    {
                        _controller.DeleteItems(
                            Enumerable.Range(e.OldStartingIndex, e.OldItems.Count)
                        );
                    }
                    else
                        ShiftPosition(-e.OldItems.Count);

                    break;

                case NotifyCollectionChangedAction.Replace:
                    _controller.ReloadItems(
                        Enumerable.Range(e.OldStartingIndex, e.OldItems.Count)
                    );
                    break;

                case NotifyCollectionChangedAction.Reset:
                    _controller.ReloadData();
                    break;

                default:
                    throw new Exception();
            }
        }

        private void ShiftPosition(int offset)
        {
            // By default the position remains the same which causes an animation in the case
            // of the added/removed position preceding the current position. I prefer the constructed
            // Android behavior whereby the item remains the same and the position changes.
            var position = _position + offset;
            _controller.ReloadData(position);
            OnPositionChange(position);
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Element.Position) && _position != Element.Position && !_carouselController.IgnorePositionUpdates)
                ScrollToPosition(Element.Position, animated: true);

            if (e.PropertyName == nameof(Element.ItemsSource))
            {
                _position = Element.Position;
                _controller.ReloadData(_position);
            }

            base.OnElementPropertyChanged(sender, e);
        }

        protected override void OnElementChanged(ElementChangedEventArgs<CarouselViewForms.CarouselView> e)
        {
            base.OnElementChanged(e);

            CarouselViewForms.CarouselView oldElement = e.OldElement;
            CarouselViewForms.CarouselView newElement = e.NewElement;
            if (oldElement != null)
                ((ICarouselViewController)oldElement).CollectionChanged -= OnCollectionChanged;

            if (newElement != null)
            {
                if (Control == null)
                    Initialize();

                // initialize properties
                _position = Element.Position;

                // hook up crud events
                ((ICarouselViewController)newElement).CollectionChanged += OnCollectionChanged;

                ReloadData(Element.Position);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                if (Element != null)
                    ((ICarouselViewController)Element).CollectionChanged -= OnCollectionChanged;
            }

            base.Dispose(disposing);

            var control = Control;
            if (disposing)
            {
                if (Controller != null)
                {
                    Controller.Dispose();
                }
                if (control != null)
                {
                    control.Dispose();
                }
            }
        }

        /// <summary>
        /// Reload the data in the controller
        /// </summary>
        /// <param name="position">current position in the data</param>
        protected void ReloadData(int position)
        {
            var parameters = new object[1];
            parameters[0] = position;
            ControllerReloadDataMethod?.Invoke(Controller, parameters);
        }

        /// <summary>
        /// Scroll to a position on the controller
        /// </summary>
        /// <param name="position">position to scroll to</param>
        /// <param name="animate">show animations when scrolling</param>
        protected void ScrollToPosition(int position, bool animated)
        {
            var parameters = new object[2];
            parameters[0] = position;
            parameters[1] = animated;
            ControllerScrollToPositionMethod?.Invoke(Controller, parameters);
        }

        public override SizeRequest GetDesiredSize(double widthConstraint, double heightConstraint)
        {
            return Control.GetSizeRequest(widthConstraint, heightConstraint, DefaultMinimumDimension, DefaultMinimumDimension);
        }

        /// <summary>
        /// This override will adjust the layout on rotation (code in the base implementation doesn't work)
        /// </summary>
        public override void LayoutSubviews()
        {
            bool? wasPortrait = null;
            if (_lastBounds != null)
            {
                wasPortrait = _lastBounds.Value.Height > _lastBounds.Value.Width;
            }
            base.LayoutSubviews();
            var nowPortrait = Bounds.Height > Bounds.Width;
            if (wasPortrait == null || wasPortrait != nowPortrait)
            {
                ScrollToPosition(Element.Position, false);
            }
            _lastBounds = Bounds;
        }
    }
}