using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/* Classe rapatriée du package Xamarin.Forms.CarouselView v2.3.0-pre2
 * Tout en faisant la mise à jour, on évite d'utiliser le CarouselView de
 * Xamarin.Forms v3.6 car il est basé sur CollectionView qui est encore experimental.
 */

namespace CarouselViewForms
{
    internal static class EnumerableExtensions
    {
        internal static IReadOnlyList<object> ToReadOnlyList(this IEnumerable enumerable)
        {
            var readOnlyList = enumerable as IReadOnlyList<object>;
            if (readOnlyList != null)
                return readOnlyList;

            var list = enumerable as IList;
            if (list != null)
                return new ListAsReadOnlyList(list);

            var objectList = enumerable as IList<object>;
            if (objectList != null)
                return new GenericListAsReadOnlyList<object>(objectList);

            // allow IList<AnyType> without falling through to the array copy below
            var typedList = (IReadOnlyList<object>)(
                from iface in enumerable.GetType().GetTypeInfo().ImplementedInterfaces
                where iface.Name == typeof(IList<>).Name && iface.GetGenericTypeDefinition() == typeof(IList<>)
                let type = typeof(GenericListAsReadOnlyList<>).MakeGenericType(iface.GenericTypeArguments[0])
                select Activator.CreateInstance(type, enumerable)
            ).FirstOrDefault();
            if (typedList != null)
                return typedList;

            // ToArray instead of ToList to save memory
            return enumerable.Cast<object>().ToArray();
        }

        private class ListAsReadOnlyList : IReadOnlyList<object>
        {
            private IList _list;

            public int Count => _list.Count;

            public object this[int index] => _list[index];

            internal ListAsReadOnlyList(IList list)
            {
                _list = list;
            }

            public IEnumerator<object> GetEnumerator() => _list.Cast<object>().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private class GenericListAsReadOnlyList<T> : IReadOnlyList<object>
        {
            private IList<T> _list;

            public int Count => _list.Count;

            public object this[int index] => _list[index];

            public GenericListAsReadOnlyList(IList<T> list)
            {
                _list = list;
            }

            public IEnumerator<object> GetEnumerator() => _list.Cast<object>().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}