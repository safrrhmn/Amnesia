﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Transactions;
using System.Threading;

namespace Amnesia
{
	public class Session : IDisposable
	{
		static TimeSpan TransactionTimeout = TimeSpan.Zero;

		public string ID;

		internal TransactionScope TxScope;
		string serviceUrl;
		EventHandler onAbortedAsync;
		bool wasAbortedAsync = false;
		private bool isDisposed;


		#region AbortNotification
		class AbortNotification : IEnlistmentNotification
		{
			EventHandler handler;

			public AbortNotification(EventHandler handler)
			{
				this.handler = handler;
			}

			public void Commit(Enlistment enlistment)
			{
				enlistment.Done();
			}

			public void InDoubt(Enlistment enlistment)
			{
				enlistment.Done();
			}

			public void Prepare(PreparingEnlistment preparingEnlistment)
			{
				preparingEnlistment.Done();
			}

			public void Rollback(Enlistment enlistment)
			{
				handler(this, EventArgs.Empty);
				enlistment.Done();
			}
		}
		#endregion

		public Session(Uri appUrl)
			: this(appUrl.ToString())
		{
		}

		public Session(string appUrl)
		{
			this.serviceUrl = appUrl + Amnesia.Settings.Current.HandlerPath;

			// Start a new distributed transaction
			TxScope = new TransactionScope(TransactionScopeOption.Required, TransactionTimeout);
			var request = new Handler.StartSessionRequest();
			Transaction tx = Transaction.Current;
			request.Transaction = tx;

			var response = request.Send(serviceUrl);
			ID = response.SessionID;

			// Monitor the distributed transaction in order to detect if its aborted remotely.
			tx.EnlistVolatile(new AbortNotification((o, e) =>
			{
				if (isDisposed)
					return;

				if (request.Transaction.TransactionInformation.Status == TransactionStatus.Aborted)
				{
					wasAbortedAsync = true;
					if(onAbortedAsync != null)
						onAbortedAsync(this, EventArgs.Empty);
				}
			}),
			EnlistmentOptions.None);
		}

		/// <summary>
		/// Indicates if the current thread is associated with a session
		/// </summary>
		public static bool IsActive
		{
			get { return Module.Transaction != null; }
		}

		/// <summary>
		/// Raised when the session is ended.  This event will be raised by
		/// a thread other than the one that created the session and can be 
		/// raised at anytime without warning.
		/// </summary>
		public event EventHandler AbortedAsync
		{
			add { onAbortedAsync += value; }
			remove { onAbortedAsync -= value; }
		}

		/// <summary>
		/// True if the session has been aborted by a remote participate in the distributed transaction
		/// </summary>
		public bool WasAbortedAsync
		{
			get { return wasAbortedAsync; }
		}

		public void Dispose()
		{
			isDisposed = true;

			// transaction is completing due to local code so disable the AbortedAsync event
			onAbortedAsync = null;

			// Tear down the local transaction scope.
			Transaction.Current.Rollback();			
			TxScope.Dispose();

			// Notify the server of the end of the session
			(new Handler.EndSessionRequest() { SessionID = ID }).Send(serviceUrl);
		}
	}
}
