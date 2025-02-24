﻿using System;
using System.Collections;
using System.Runtime.CompilerServices;
using Xamarin.Forms;

/* Classe rapatriée du package Xamarin.Forms.CarouselView v2.3.0-pre2
 * Tout en faisant la mise à jour, on évite d'utiliser le CarouselView de
 * Xamarin.Forms v3.6 car il est basé sur CollectionView qui est encore experimental.
 */

namespace CarouselViewForms
{
    public abstract class BindingBase
    {
        private static readonly ConditionalWeakTable<IEnumerable, CollectionSynchronizationContext> SynchronizedCollections = new ConditionalWeakTable<IEnumerable, CollectionSynchronizationContext>();

        private BindingMode _mode = BindingMode.Default;
        private string _stringFormat;
        private object _targetNullValue;
        private object _fallbackValue;

        internal bool AllowChaining { get; set; }

        internal object Context { get; set; }

        internal bool IsApplied { get; private set; }

        public BindingMode Mode
        {
            get { return _mode; }
            set
            {
                if (value != BindingMode.Default
                    && value != BindingMode.OneWay
                    && value != BindingMode.OneWayToSource
                    && value != BindingMode.TwoWay
                    && value != BindingMode.OneTime)
                    throw new ArgumentException("mode is not a valid BindingMode", "mode");

                ThrowIfApplied();

                _mode = value;
            }
        }

        public string StringFormat
        {
            get { return _stringFormat; }
            set
            {
                ThrowIfApplied();

                _stringFormat = value;
            }
        }

        public object TargetNullValue
        {
            get { return _targetNullValue; }
            set
            {
                ThrowIfApplied();
                _targetNullValue = value;
            }
        }

        public object FallbackValue
        {
            get => _fallbackValue;
            set
            {
                ThrowIfApplied();
                _fallbackValue = value;
            }
        }

        internal BindingBase()
        {
        }

        protected void ThrowIfApplied()
        {
            if (IsApplied)
                throw new InvalidOperationException("Can not change a binding while it's applied");
        }

        internal static bool TryGetSynchronizedCollection(IEnumerable collection, out CollectionSynchronizationContext synchronizationContext)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            return SynchronizedCollections.TryGetValue(collection, out synchronizationContext);
        }

        internal virtual void Apply(bool fromTarget)
        {
            IsApplied = true;
        }

        internal virtual void Apply(object context, BindableObject bindObj, BindableProperty targetProperty, bool fromBindingContextChanged = false)
        {
            IsApplied = true;
        }

        internal abstract BindingBase Clone();

        internal virtual object GetSourceValue(object value, Type targetPropertyType)
        {
            if (value == null && TargetNullValue != null)
                return TargetNullValue;

            if (StringFormat != null)
                return string.Format(StringFormat, value);

            return value;
        }

        internal virtual object GetTargetValue(object value, Type sourcePropertyType)
        {
            return value;
        }

        internal virtual void Unapply(bool fromBindingContextChanged = false)
        {
            IsApplied = false;
        }

        public static void DisableCollectionSynchronization(IEnumerable collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            SynchronizedCollections.Remove(collection);
        }

        public static void EnableCollectionSynchronization(IEnumerable collection, object context, CollectionSynchronizationCallback callback)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            SynchronizedCollections.Add(collection, new CollectionSynchronizationContext(context, callback));
        }
    }
}