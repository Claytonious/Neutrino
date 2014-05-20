using System;
using System.Collections.Generic;

namespace Neutrino.Core
{
	public class ConcurrentPool<T> where T : new()
	{
		private Stack<T> items = new Stack<T>();

		public ConcurrentPool(int initialCapacity)
		{
			items = new Stack<T>(initialCapacity);
		}

		public T Pop()
		{
			T result = default(T);
			bool popped = false;
			lock (items)
			{
				if (items.Count > 0)
				{
					result = items.Pop();
					popped = true;
				}
			}
			if (!popped)
				result = new T();
			return result;
		}

		public void Push(T item)
		{
			lock(items)
				items.Push(item);
		}
	}
}
