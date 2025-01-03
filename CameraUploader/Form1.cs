﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CameraUploader
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        string ftpusername;
        string ftppassword;
        string ftpaddress;
        string ftpaddressext;
        List<CameraInfo> cameras;
        List<string> exturls;
        int runs = 0;

        private void Form1_Load(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;

            int timer = int.Parse(System.Configuration.ConfigurationManager.AppSettings["timer"]);
            InitCameras();

            if (timer > 0)
            {
                timer1.Interval = timer * 1000;
                timer1.Start();
                label1.Text = "Timer every " + timer + "s";
            }
            else
                label1.Text = "No timer";
        }

        private void InitCameras()
        {
            ftpusername = System.Configuration.ConfigurationManager.AppSettings["ftpusername"];
            ftppassword = System.Configuration.ConfigurationManager.AppSettings["ftppassword"];
            ftpaddress = System.Configuration.ConfigurationManager.AppSettings["ftpaddress"];

            int noOfCameras = int.Parse(System.Configuration.ConfigurationManager.AppSettings["NoOfCameras"]);
            cameras = new List<CameraInfo>();
            for (int i = 1; i <= noOfCameras; i++)
            {
                var ci = new CameraInfo()
                {
                    Address = System.Configuration.ConfigurationManager.AppSettings["camaddress" + i],
                    Username = System.Configuration.ConfigurationManager.AppSettings["camusername" + i],
                    Password = System.Configuration.ConfigurationManager.AppSettings["campassword" + i],
                    Filename = System.Configuration.ConfigurationManager.AppSettings["camfilename" + i],
                    Name = System.Configuration.ConfigurationManager.AppSettings["camname" + i],
                    Backup = System.Configuration.ConfigurationManager.AppSettings["cambackup" + i] == "1" || bool.Parse(System.Configuration.ConfigurationManager.AppSettings["cambackup" + i]),
                    BackupFolder = System.Configuration.ConfigurationManager.AppSettings["cambackupfolder" + i],
                    Type = int.Parse(System.Configuration.ConfigurationManager.AppSettings["camtype" + i]),
                };
                cameras.Add(ci);

                if (!string.IsNullOrWhiteSpace(ci.BackupFolder))
                {
                    if(!System.IO.Directory.Exists(ci.BackupFolder))
                        System.IO.Directory.CreateDirectory(ci.BackupFolder);
                }
            }

            textBox1.Text = "";
            WriteLog("Number of cameras: " + cameras.Count());

            exturls = System.Configuration.ConfigurationManager.AppSettings["extcamurls"].Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
            ftpaddressext = System.Configuration.ConfigurationManager.AppSettings["ftpaddressext"];
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            ProcessCameras();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            WriteLog("Timer");
            ProcessCameras();
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            notifyIcon1.Visible = false;
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                notifyIcon1.Visible = true;
                Hide();
            }
        }

        private void ProcessCameras()
        {
            label1.Text = "Start: " + DateTime.Now;
            WriteLog(label1.Text);

            foreach (var ci in cameras)
            {
                WriteLog(ci.Name);
                var result = GetAndUploadImage(ci.Address, ci.Type, ci.Username, ci.Password, ci.Filename, ci.Name, ci.Backup, ci.BackupFolder);
            }

            foreach (var url in exturls)
            {
                WriteLog(url);
                var result = GetAndUploadImageExt(url);
            }

            runs++;

            label1.Text += "... End: " + DateTime.Now + " (" + runs + ")";
            WriteLog("End: " + DateTime.Now + " (" + runs + ")");

            notifyIcon1.Text = label1.Text;
        }

        private string GetCameraUrl(int type, string address, string camusername, string campwd)
        {
            switch (type)
            {
                case 1:
                    {
                        //Foscam
                        return string.Format("http://{0}/cgi-bin/CGIProxy.fcgi?cmd=snapPicture2&usr={1}&pwd={2}", address, camusername, campwd);
                    }
                case 2:
                    {
                        //Hilook B150H-M
                        return $"http://{address}//ISAPI/Streaming/channels/101/picture?videoResolutionWidth=1920&videoResolutionHeight=1080";
                    }
                default: return "";
            }
        }

        private bool GetAndUploadImage(string address, int type, string camusername, string campwd, string destFileName, string name, bool backup, string backupFolder)
        {
            try
            {
                string url = GetCameraUrl(type, address, camusername, campwd);
              
                using (WebClient client = new WebClient())
                {
                    if (type == 2)
                    {
                        client.Credentials = new NetworkCredential(camusername, campwd);
                    }
                    client.DownloadFile(url, destFileName);
                }

                using (WebClient client = new WebClient())
                {
                    client.Credentials = new NetworkCredential(ftpusername, ftppassword);
                    client.UploadFile(ftpaddress + destFileName, WebRequestMethods.Ftp.UploadFile, destFileName);
                }

                if (backup)
                {
                    System.IO.File.Copy(destFileName, System.IO.Path.Combine(backupFolder??"", destFileName.Replace(".", "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".")));
                    //ffmpeg -r 24 -i cam%d.jpg -s hd1080 timelapse.mp4 
                }

                return true;
            }
            catch (Exception exc)
            {
                WriteLog("Error:" + name + ":" + exc.Message);
                return false;
            }
        }

        private bool GetAndUploadImageExt(string url)
        {
            try
            {
                string destFileName = url.Substring(url.LastIndexOf("/") + 1);

                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(url, destFileName);
                }

                using (WebClient client = new WebClient())
                {
                    client.Credentials = new NetworkCredential(ftpusername, ftppassword);
                    client.UploadFile(ftpaddressext + destFileName, WebRequestMethods.Ftp.UploadFile, destFileName);
                }

                return true;
            }
            catch (Exception exc)
            {
                WriteLog("Error:" + url + ":" + exc.Message);
                return false;
            }
        }

        private void WriteLog(string msg)
        {
            textBox1.Text += msg + System.Environment.NewLine;
            System.IO.File.AppendAllText("CameraUploader.log", msg + System.Environment.NewLine);
        }

    }

    class CameraInfo
    {
        public string Address { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Filename { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
        public bool Backup { get; set; }
        public string BackupFolder { get; set; }
    }
}
