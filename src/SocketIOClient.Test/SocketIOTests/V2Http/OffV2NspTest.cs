﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace SocketIOClient.Test.SocketIOTests.V2Http
{
    [TestClass]
    public class OffV2NspTest : OffTest
    {
        public OffV2NspTest()
        {
            SocketIOCreator = new SocketIOV2NspCreator();
        }

        protected override ISocketIOCreateable SocketIOCreator { get; }

        [TestMethod]
        public override async Task Test()
        {
            await base.Test();
        }
    }
}
