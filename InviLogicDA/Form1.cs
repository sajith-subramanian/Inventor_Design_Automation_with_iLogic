using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

namespace InviLogicDA
{
    public partial class Form1 : Form
    {
        public ChromiumWebBrowser browser;
        public Form1()
        {
            InitializeComponent();
            InitBrowser();
        }

        public void InitBrowser()
        {
            Cef.Initialize(new CefSettings());

            browser = new ChromiumWebBrowser("file:///HTML/Viewer.html")
            {
                Dock = DockStyle.Fill,

            };
            splitContainer1.Panel2.Controls.Add(browser);
        }

        private async void btnPreview_Click(object sender, EventArgs e)
        {
            try
            {
                switch (cbColor.Text)
                {
                    case "Chrome":
                        DAUtils.paramColor = 1;
                        break;
                    case "Brass":
                        DAUtils.paramColor = 2;
                        break;
                    case "Copper":
                        DAUtils.paramColor = 3;
                        break;
                }
                switch (cbRim.Text)
                {
                    case "5 spoke":
                        DAUtils.paramRim = 1;
                        break;
                    case "Multi spoke":
                        DAUtils.paramRim = 2;
                        break;
                }
                try
                {
                    this.UseWaitCursor = true;
                    this.splitContainer1.Panel1.Enabled = false;

                    Console.WriteLine("Fetching internal token...");
                    DAUtils.InternalToken = await DAUtils.GetInternalAsync();
                    try
                    {
                        Console.WriteLine("Creating bucket...");
                        dynamic bucketkey = await DAUtils.CreateBucket();
                        try
                        {
                            Console.WriteLine("Uploading file...");
                            dynamic uploadedobject = await DAUtils.UploadIptFile(bucketkey);
                            try
                            {
                                Console.WriteLine("Creating Activity...");
                                await DAUtils.CreateActivity();
                                try
                                {
                                    Console.WriteLine("Creating workitem...");
                                    dynamic obj = await DAUtils.CreateWorkItem(bucketkey);
                                    try
                                    {
                                        Console.WriteLine("Translating Zip file...");
                                        dynamic translatedobject = await DAUtils.TranslateIptFile(obj);

                                        Console.WriteLine("Opening document in browser...");
                                        browser.Load(string.Format("file:///HTML/Viewer.html?URN={0}&Token={1}", translatedobject.urn, DAUtils.InternalToken.access_token));
                                    }
                                    catch (Exception ex) { Console.WriteLine("Translation failed: " + ex.Message); }
                                }
                                catch (Exception ex) { Console.WriteLine("Work item failed: " + ex.Message); }
                            }
                            catch (Exception ex) { Console.WriteLine("Activity failed: " + ex.Message); }
                        }
                        catch (Exception ex) { Console.WriteLine("Upload File failed: " + ex.Message); }
                    }
                    catch (Exception ex) { Console.WriteLine("CreateBucket failed: " + ex.Message); }
                }
                catch (Exception ex) { Console.WriteLine("GetInternalAsync failed: " + ex.Message); }
            }
            catch (Exception ex) { Console.WriteLine("CreateZipFile failed: " + ex.Message); }

            finally
            {
                this.UseWaitCursor = false;
                this.splitContainer1.Panel1.Enabled = true;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Cef.Shutdown();
        }

        /// <summary>
        /// Downloads the Result.ipt file to the current user's Documents folder 
        /// </summary> 
        private async void btnDownload_Click(object sender, EventArgs e)
        {
           await DAUtils.DownloadToDocs();
        }
    }      
}


