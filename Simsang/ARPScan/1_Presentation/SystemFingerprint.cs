﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml;
using System.Windows.Forms;

using Simsang.ARPScan.Main.Config;


namespace Simsang.ARPScan.SystemFingerprint
{
  public partial class SystemFingerprint : Form
  {

    #region MEMBERS

    private TaskFacadeFingerprint cTaskFingerprint;
    private String mOperatingSystem;
    private String mScanDate;
    private String mMACAddress;
    private String mIPAddress;
    private String mMACHardwareVendor;

    #endregion


    #region PUBLIC

    public SystemFingerprint(String pMAC, String pIP, String pHWVendor)
    {
      InitializeComponent();

      mMACAddress = pMAC;
      mMACHardwareVendor = pHWVendor;
      mIPAddress = pIP;
      cTaskFingerprint = TaskFacadeFingerprint.getInstance();

      this.Text = String.Format("{0} / {1}", pIP, pMAC);
      loadSystemDetails();
    }

    #endregion


    #region EVENTS

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BT_Close_Click(object sender, EventArgs e)
    {
      // Stopping scan process
      cTaskFingerprint.stopFingerprint();

      // Resetting GUI elements
      activateGUIElements();

      // Hiding form
      this.Hide();
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BT_Scan_Click(object sender, EventArgs e)
    {
      try
      {
        FingerprintConfig lScanConfig = new FingerprintConfig()
        {
          IP = mIPAddress,
          MAC = mMACAddress,
          IsDebuggingOn = Simsang.Config.DebugOn(),
          OnScanStopped = FingerprintStopped
        };

        deactivateGUIElements();
        cTaskFingerprint.startFingerprint(lScanConfig);
      }
      catch (Exception lEx)
      {
        LogConsole.Main.LogConsole.pushMsg(String.Format("Fingerprint : {0}", lEx.Message));
      }
    }


    /// <summary>
    /// Close Sessions GUI on Escape.
    /// </summary>
    /// <param name="keyData"></param>
    /// <returns></returns>
    protected override bool ProcessDialogKey(Keys keyData)
    {
      if (keyData == Keys.Escape)
      {
        cTaskFingerprint.stopFingerprint();
        this.Close();
        return true;
      }
      else
        return base.ProcessDialogKey(keyData);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SystemFingerprint_FormClosing(object sender, FormClosingEventArgs e)
    {
      // Stopping scan process
      cTaskFingerprint.stopFingerprint();

      // Resetting GUI elements
      activateGUIElements();

      // Hiding form
      this.Hide();
      e.Cancel = true;
    }


    #endregion


    #region PRIVATE

    /// <summary>
    /// 
    /// </summary>
    public delegate void deactivateGUIElementsDelegate();
    private void deactivateGUIElements()
    {
      if (InvokeRequired)
      {
        BeginInvoke(new deactivateGUIElementsDelegate(deactivateGUIElements), new object[] { });
        return;
      }

      this.Cursor = Cursors.WaitCursor;

      BT_Close.Enabled = false;
      BT_Scan.Enabled = false;

      TB_OSGuess.Cursor = Cursors.WaitCursor;
      TB_OpenPorts.Cursor = Cursors.WaitCursor;
      TB_HWVendor.Cursor = Cursors.WaitCursor;
      TB_MAC.Cursor = Cursors.WaitCursor;
      TB_ScanDate.Cursor = Cursors.WaitCursor;
    }


    /// <summary>
    /// 
    /// </summary>
    public delegate void activateGUIElementsDelegate();
    private void activateGUIElements()
    {
      if (InvokeRequired)
      {
        BeginInvoke(new activateGUIElementsDelegate(activateGUIElements), new object[] { });
        return;
      }
      this.Cursor = Cursors.Default;

      BT_Close.Enabled = true;
      BT_Scan.Enabled = true;

      TB_OSGuess.Cursor = Cursors.Default;
      TB_OpenPorts.Cursor = Cursors.Default;
      TB_HWVendor.Cursor = Cursors.Default;
      TB_MAC.Cursor = Cursors.Default;
      TB_ScanDate.Cursor = Cursors.Default;
    }



    /// <summary>
    /// 
    /// </summary>
    public delegate void loadSystemDetailsDelegate();
    private void loadSystemDetails()
    {
      if (InvokeRequired)
      {
        BeginInvoke(new loadSystemDetailsDelegate(loadSystemDetails), new object[] { });
        return; 
      }

      TB_HWVendor.Text = mMACHardwareVendor;
      TB_MAC.Text = mMACAddress;


      String lFilePath = cTaskFingerprint.getSystenDetailsFile(mMACAddress);
      String lPorts = "\r\n";
      String lOSGuess = "\r\n";
      var xdoc = new XDocument();

      try
      {
        xdoc = XDocument.Load(lFilePath);
        TB_ScanDate.Text = xdoc.Descendants().Elements("runstats").First().Element("finished").Attribute("timestr").Value;
      }
      catch (Exception lEx) 
      {
        String lMessage = lEx.Message;
      }



      /*
       * Extract open ports from XML file
       */
      try
      {
        var ports = xdoc.Descendants().Elements("port")
                       .Select(y => new
                       {
                         Protocol = y.Attribute("protocol").Value,
                         PortNo = y.Attribute("portid").Value,
                         ServiceName = y.Element("service").Attribute("name").Value
                       });

        int lCount = 0;
        foreach (var entry in ports)
        {
          if (lCount >= 7)
            break;

          lPorts += String.Format("   {0}/{1,-5} {2}\r\n", entry.Protocol, entry.PortNo, entry.ServiceName);
          lCount++;
        } // foreach(va...

        TB_OpenPorts.Text = lPorts;
      }
      catch (Exception) { }


      /*
       * Extract OS guess from XML file
       */
      try
      {
        var OSGuess = xdoc.Descendants().Elements("os").First().Elements("osmatch")
                       .Select(y => new
                       {
                         Accuracy = y.Attribute("accuracy").Value,
                         OSName = y.Attribute("name").Value
                       });

        foreach (var entry in OSGuess)
          lOSGuess += String.Format(" {0} ({1}%)\r\n", entry.OSName, entry.Accuracy);

        TB_OSGuess.Text = lOSGuess;
      }
      catch (Exception lEx) 
      {
        String lMsg = lEx.Message;
      }

    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="pMAC"></param>
    /// <returns></returns>
    private String getFileNameByMAC(String pMAC)
    {
      String lOutputFileName = String.Empty;
      String lFingerprintDir = String.Empty;
      String lMACAddr = String.Empty;

      if (!String.IsNullOrEmpty(pMAC))
      {
        lFingerprintDir = String.Format(@"{0}\{1}", Directory.GetCurrentDirectory(), Simsang.Config.FingerprintDir);
        lMACAddr = Regex.Replace(pMAC, @"[^\d\w]", "");
        lOutputFileName = String.Format(@"{0}\{1}.xml", lFingerprintDir, lMACAddr);
      } // if (!Strin...

      return (lOutputFileName);
    }


    /// <summary>
    /// 
    /// </summary>
    private void FingerprintStopped()
    {
      activateGUIElements();
      loadSystemDetails();
    }

    #endregion

  }
}
