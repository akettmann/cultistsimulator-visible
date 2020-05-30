﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Core.Fucine
{
    public class ContentImportLogger
    {
        private IList<ContentImportMessage> _contentImportMessages= new List<ContentImportMessage>();

        public void LogProblem(string problemDesc)
        {
            _contentImportMessages.Add(new ContentImportMessage(problemDesc));
        }

        public void LogInfo(string desc)
        {
            _contentImportMessages.Add(new ContentImportMessage(desc,0));
        }

        public IList<ContentImportMessage> GetMessages()
        {
            return new List<ContentImportMessage>(_contentImportMessages);

        }
    }
}
