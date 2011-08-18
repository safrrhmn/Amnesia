﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amnesia
{
	[Serializable]
	class SerializableLog : ILog
	{
		[NonSerialized]
		DateTime startTime = DateTime.Now;

		List<string> entries = new List<string>();

		public void Write(string messageFormat, params object[] args)
		{
			entries.Add(string.Format("[{0}] ", (DateTime.Now - startTime)) + " " + string.Format(messageFormat, args));
		}

		public IEnumerable<string> Entries
		{
			get { return entries; }
		}
	}

}