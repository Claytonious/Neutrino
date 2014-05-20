using System;
using System.Collections.Generic;

namespace Neutrino.Core
{
	public class Pool<T> where T : new()
	{
		private Stack<T> items = new Stack<T>();

		public Pool(int initialCapacity)
		{
			items = new Stack<T>(initialCapacity);
		}

		public T Pop()
		{
			T result = default(T);
			if (items.Count > 0)
				result = items.Pop();
			else
				result = new T();
			return result;
		}

		public void Push(T item)
		{
			items.Push(item);
		}
	}
}
