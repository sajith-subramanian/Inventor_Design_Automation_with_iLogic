using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Inventor;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;
using System.IO;


namespace UpdateParams
{
    [ComVisible(true)]
    public class SampleAutomation
    {
        InventorServer m_inventorServer;

        public SampleAutomation(InventorServer inventorServer)
        {
            Trace.TraceInformation("Starting sample plugin.");
            m_inventorServer = inventorServer;
        }

        public void Run(Document doc)
        {

            Trace.TraceInformation("Running with no Args.");
            NameValueMap map = m_inventorServer.TransientObjects.CreateNameValueMap();
            RunWithArguments(doc, map);

        }

        public void RunWithArguments(Document doc, NameValueMap map)
        {
            try
            {
                StringBuilder traceInfo = new StringBuilder("RunWithArguments called with ");
                traceInfo.Append(doc.DisplayName);
                Trace.TraceInformation(map.Count.ToString());
                // values in map are keyed on _1, _2, etc
                for (int i = 0; i < map.Count; i++)
                {
                    traceInfo.Append(" and ");
                    traceInfo.Append(map.Value["_" + (i + 1)]);
                }
                Trace.TraceInformation(traceInfo.ToString());

                #region change parameters
                Trace.TraceInformation("Changing User params");

                // load processing parameters
                string paramsJson = GetParametersToChange(map);
                Trace.TraceInformation("Inventor Parameters JSON: \"" + paramsJson + "\"");

                var theParams = GetParameters(doc);

                Dictionary<string, string> parameters = JsonConvert.DeserializeObject<Dictionary<string, string>>(paramsJson);
                foreach (KeyValuePair<string, string> entry in parameters)
                {
                    var parameterName = entry.Key;
                    var value = entry.Value;
                    Trace.TraceInformation("Parameter to change: {0}:{1}", parameterName, value);
                    try
                    {
                        UserParameter param = theParams[parameterName];
                        param.Value = value;
                    }
                    catch (Exception e)
                    {
                        Trace.TraceInformation("Cannot update '{0}' parameter. ({1})", parameterName, e.Message);
                    }
                }
                doc.Update();
                Trace.TraceInformation("Doc updated.");

                var docDir = System.IO.Path.GetDirectoryName(doc.FullFileName);
                var fileName = System.IO.Path.Combine(docDir, "Result.ipt"); // the name must be in sync with OutputIpt localName in Activity
                doc.SaveAs(fileName, false);
                Trace.TraceInformation("Doc saved.");
                #endregion
            }
            catch (Exception ex)
            { Trace.TraceInformation(ex.Message); }
        }

        private static string GetParametersToChange(NameValueMap map)
        {
            string paramFile = (string)map.Value["_1"];
            string json = System.IO.File.ReadAllText(paramFile);
            return json;
        }

        private static UserParameters GetParameters(Document doc)
        {
            var docType = doc.DocumentType;
            switch (docType)
            {
                case DocumentTypeEnum.kAssemblyDocumentObject:
                    var asm = doc as AssemblyDocument;
                    return asm.ComponentDefinition.Parameters.UserParameters;

                case DocumentTypeEnum.kPartDocumentObject:
                    var ipt = doc as PartDocument;
                    return ipt.ComponentDefinition.Parameters.UserParameters;

                default:
                    throw new ApplicationException(string.Format("Unexpected document type ({0})", docType));
            }
        }        
    }
}
