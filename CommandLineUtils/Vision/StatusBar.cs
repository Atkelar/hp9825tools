using System;
using System.Diagnostics.CodeAnalysis;

namespace CommandLineUtils.Visuals
{
    public class StatusBar
    {
        public static StatusBarBuilder Make()
        {
            throw new NotImplementedException();
        }
    }

    public class StatusBarBuilder
    {
        // public StatusBarBuilder Add(StatusBarItem item)
        // {
        //     throw new NotImplementedException();
        // }

        /// <summary>
        /// Adds a menu-item type of status info.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        public StatusBarBuilder Add(string name, string command)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addStatusText">True adds a status text panel to the end of the list of items.</param>
        /// <returns></returns>
        public StatusBar Build(bool addStatusText = true)
        {
            throw new NotImplementedException();
        }
    }
}
