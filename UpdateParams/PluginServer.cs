using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Inventor;

namespace UpdateParams
{
    [Guid("92DDE0B3-A219-4C18-B28F-F2136738F475")]
    public class PluginServer : ApplicationAddInServer
    {
        public PluginServer()
        {
        }

        // Inventor application object.
        InventorServer m_inventorServer;
        SampleAutomation m_automation;

        public dynamic Automation
        {
            get
            {
                return m_automation;
            }
        }

        public void Activate(ApplicationAddInSite AddInSiteObject, bool FirstTime)
        {
            Trace.TraceInformation("Update Param Plugin: initializing... ");

            // Initialize AddIn members.
            m_inventorServer = AddInSiteObject.InventorServer;
            m_automation = new SampleAutomation(m_inventorServer);
        }

        public void Deactivate()
        {
            Trace.TraceInformation("Update Param Plugin: deactivating... ");

            // Release objects.
            Marshal.ReleaseComObject(m_inventorServer);
            m_inventorServer = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void ExecuteCommand(int CommandID)
        {
            // obsolete
        }
    }
}
