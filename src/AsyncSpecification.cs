﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Xunit.Extensions
{
	/// <summary>
	/// The base async specification class
	/// </summary>
	public abstract class AsyncSpecification : IAsyncLifetime
	{
		Exception exception;
		static readonly ReaderWriterLockSlim sync = new ReaderWriterLockSlim();
		static readonly Dictionary<Type, bool> typeCache = new Dictionary<Type, bool>();

		/// <summary>
		/// The exception that was thrown when Observe was run; null if no exception was thrown.
		/// </summary>
		protected Exception ThrownException => exception;

		/// <summary>
		/// Initialize the test class all async-like.
		/// </summary>
		protected virtual Task InitializeAsync() => CommonTasks.Completed;

		/// <summary>
		/// Performs the action to observe the outcome of to validate the specification.
		/// </summary>
		protected abstract Task ObserveAsync();

		/// <summary>
		/// Cleanup the test class all async-like.
		/// </summary>
		protected virtual Task DisposeAsync() => CommonTasks.Completed;

		Task IAsyncLifetime.DisposeAsync() => DisposeAsync();

		async Task IAsyncLifetime.InitializeAsync()
		{
			await InitializeAsync();

			try
			{
				await ObserveAsync();
			}
			catch (Exception ex)
			{
				if (!HandleException(ex))
					throw;
			}
		}

		bool HandleException(Exception ex)
		{
			exception = ex;
			return ShouldHandleException();
		}

		bool ShouldHandleException()
		{
			Type type = GetType();

			try
			{
				sync.EnterReadLock();

				if (typeCache.ContainsKey(type))
					return typeCache[type];
			}
			finally
			{
				sync.ExitReadLock();
			}

			try
			{
				sync.EnterWriteLock();

				if (typeCache.ContainsKey(type))
					return typeCache[type];

				var attrs = type.GetTypeInfo().GetCustomAttributes(typeof(HandleExceptionsAttribute), true).OfType<HandleExceptionsAttribute>();

				return typeCache[type] = attrs.Any();
			}
			finally
			{
				sync.ExitWriteLock();
			}
		}
	}
}