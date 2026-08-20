using System.Threading;
using System;
using System.Collections.Concurrent;

namespace cakeslice.SimpleWebRTC
{
	public static class Common
	{
		private static readonly ConcurrentQueue<Action> MainThreadQueue = new ConcurrentQueue<Action>();
		private static int _mainThreadId;

		public static void InitializeMainThread()
		{
			_mainThreadId = Thread.CurrentThread.ManagedThreadId;
		}

		public static void ExecuteMainThreadQueue()
		{
			while (MainThreadQueue.TryDequeue(out Action action))
				action();
		}

		public static T RunOnMainThread<T>(Func<T> action)
		{
			if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
				return action();

			using ManualResetEventSlim completed = new ManualResetEventSlim(false);
			T result = default;
			Exception exception = null;
			MainThreadQueue.Enqueue(() =>
			{
				try { result = action(); }
				catch (Exception caught) { exception = caught; }
				finally { completed.Set(); }
			});
			completed.Wait();
			if (exception != null)
				throw exception;
			return result;
		}

		public static void RunOnMainThread(Action action)
		{
			RunOnMainThread(() =>
			{
				action();
				return true;
			});
		}

		[System.Serializable]
		public class ICEServer
		{
			public string url;
			public string username;
			public string credential;
		}

		[Serializable]
		public enum DeliveryMethod : byte
		{
			Unreliable = 4,
			ReliableOrdered = 2,
		}

		public enum EventType
		{
			Connected,
			Data,
			Disconnected,
			Error
		}

		public static void CheckForInterrupt()
		{
			// sleep in order to check for ThreadInterruptedException
			Thread.Sleep(1);
		}
	}
}
