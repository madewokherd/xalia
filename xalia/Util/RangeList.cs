using System;
using System.Collections;
using System.Collections.Generic;

namespace Xalia.Util
{
    public class RangeList : IList<int>
    {
        public RangeList(int start, int end)
        {
            if (end < start)
                throw new ArgumentException($"end ({end}) must be greater than or equal to start ({start})");
            Start = start;
            End = end;
        }

        public int this[int index] { get => Start + index; }
        int IList<int>.this[int index] { get => this[index]; set => throw new NotImplementedException(); }

        public int Start { get; }
        public int End { get; }

        public int Count => End - Start;

        public bool IsReadOnly => true;

        public void Add(int item)
        {
            throw new System.NotImplementedException();
        }

        public void Clear()
        {
            throw new System.NotImplementedException();
        }

        public bool Contains(int item)
        {
            return Start <= item && item < End;
        }

        public void CopyTo(int[] array, int arrayIndex)
        {
            var count = Count;
            for (int i=0; i < count; i++)
            {
                array[arrayIndex + i] = Start + i;
            }
        }

        public IEnumerator<int> GetEnumerator()
        {
            return new RangeEnumerator(this);
        }

        public int IndexOf(int item)
        {
            if (Contains(item))
                return item - Start;
            return -1;
        }

        public void Insert(int index, int item)
        {
            throw new System.NotImplementedException();
        }

        public bool Remove(int item)
        {
            throw new System.NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new System.NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public class RangeEnumerator : IEnumerator<int>
        {
            public RangeEnumerator(RangeList range)
            {
                Range = range;
                Reset();
            }

            public RangeList Range { get; }

            public int Current { get; set; }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                Current++;
                return Current < Range.End;
            }

            public void Reset()
            {
                Current = Range.Start - 1;
            }
        }
    }
}
