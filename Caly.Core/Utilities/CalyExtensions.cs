using System;
using System.Collections;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;

namespace Caly.Core.Utilities
{
    internal static class CalyExtensions
    {
        public static T FindFromNameScope<T>(this INameScope e, string name) where T : Control
        {
            var element = e.Find<T>(name);
            return element ?? throw new NullReferenceException($"Could not find {name}.");
        }

        public static bool IsEmpty(this Rect rect)
        {
            return rect.Size.IsEmpty();
        }

        public static bool IsEmpty(this Size size)
        {
            return size.Height <= float.Epsilon || size.Width <= float.Epsilon;
        }

        /// <summary>
        /// Thread safe.
        /// </summary>
        public static void AddSafely<T>(this ObservableCollection<T> collection, T element)
        {
            var list = (IList)collection;
            lock (list.SyncRoot)
            {
                list.Add(element);
            }
        }

        /// <summary>
        /// Thread safe.
        /// </summary>
        public static void RemoveSafely<T>(this ObservableCollection<T> collection, T element)
        {
            var list = (IList)collection;
            lock (list.SyncRoot)
            {
                list.Remove(element);
            }
        }

        /// <summary>
        /// Thread safe.
        /// </summary>
        public static int IndexOfSafely<T>(this ObservableCollection<T> collection, T element)
        {
            var list = (IList)collection;
            lock (list.SyncRoot)
            {
                return list.IndexOf(element);
            }
        }

        /*
        /// <summary>
        /// Transforms the rectangle by a matrix. Only uses doubles (and not floats contrary to Avalonia's) in order to not lose precision.
        /// </summary>
        /// <param name="rect">The rectangle to transform.</param>
        /// <param name="matrix">The transform.</param>
        /// <returns>The transformed rectangle.</returns>
        public static Rect TransformSafe(this Rect rect, Matrix matrix)
        {
            if (matrix.IsIdentity)
            {
                return rect;
            }

            // We can't use Avalonia's transforms as it will convert the doubles to floats
            return new Rect(rect.TopLeft.TransformSafe(matrix), rect.BottomRight.TransformSafe(matrix));
        }

        /// <summary>
        /// Transforms the point by a matrix. Only uses doubles (and not floats contrary to Avalonia's) in order to not lose precision.
        /// </summary>
        /// <param name="point">The point to transform.</param>
        /// <param name="matrix">The transform.</param>
        /// <returns>The transformed point.</returns>
        public static Point TransformSafe(this Point point, Matrix matrix)
        {
            if (matrix.IsIdentity)
            {
                return point;
            }

            // We can't use Avalonia's transforms as it will convert the doubles to floats
            return new Point(
                (point.X * matrix.M11) + (point.Y * matrix.M21),
                (point.X * matrix.M12) + (point.Y * matrix.M22));
        }

        /// <summary>
        /// Transforms the vector by a matrix. Only uses doubles (and not floats contrary to Avalonia's) in order to not lose precision.
        /// </summary>
        /// <param name="vector">The vector to transform.</param>
        /// <param name="matrix">The transform.</param>
        /// <returns>The transformed vector.</returns>
        public static Vector TransformSafe(this Vector vector, Matrix matrix)
        {
            if (matrix.IsIdentity)
            {
                return vector;
            }

            return new Vector(
                (vector.X * matrix.M11) + (vector.Y * matrix.M21),
                (vector.X * matrix.M12) + (vector.Y * matrix.M22));
        }
        */
    }
}
