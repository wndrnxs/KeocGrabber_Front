/*
 *******************************************************************************
 * 해당 소스는 현장 유지 보수 외에 다른 목적의 사용을 금합니다.
 * - 주식회사 태루 -
 *******************************************************************************
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KEOC_ImageGrabber
{
    public class Page_SystemInfoDataContext : MVVMBase.IPropertyChanged
    {
        double dCpuVal = 0.0;
        double dRamVal = 0.0;
        double dDrive1Val = 0.0;
        double dDrive2Val = 0.0;
        
        public double CPUVal { get { return dCpuVal; } set { dCpuVal = value; OnPropertyChanged("CPUVal"); } }
        public double RAMVal { get { return dRamVal; } set { dRamVal = value; OnPropertyChanged("RAMVal"); } }
        public double Drive1Val { get { return dDrive1Val; } set { dDrive1Val = value; OnPropertyChanged("Drive1Val"); } }
        public double Drive2Val { get { return dDrive2Val; } set { dDrive2Val = value; OnPropertyChanged("Drive2Val"); } }
    }


    /// <summary>
    /// Page_SystemInfo.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Page_SystemInfo : Page
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);


        //Thread thread = null;
        public Page_SystemInfo()
        {
            InitializeComponent();

            Task task = new Task(new Action(THREAD_UPDATE));
            task.Start();

        }

        void THREAD_UPDATE()
        {
            //var cpuCounter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total");
            var cpuCounter = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
            var memCounter = new PerformanceCounter("Memory", "Available Bytes");

            DriveInfo[] listdisk = DriveInfo.GetDrives();

            DriveInfo disk1 = null;
            DriveInfo disk2 = null;

            var disk = DriveInfo.GetDrives().Where(c => c.Name.Contains("D") == true);
            
            if (disk.Count() > 0) disk1 = disk.First();

            disk = DriveInfo.GetDrives().Where(c => c.Name.Contains("E") == true);
            if (disk.Count() > 0) disk2 = disk.First();
            
            ulong installedMemory = 1;
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                installedMemory = memStatus.ullTotalPhys;
            }

            double cputotalusage, ramusage, disk1usage, disk2usage;
            while (true)
            {
                cputotalusage = cpuCounter.NextValue();
                cputotalusage = Math.Round(cputotalusage, 1);

                ramusage = ((installedMemory - memCounter.NextValue()) / installedMemory) * 100;
                ramusage = Math.Round(ramusage, 1);

                datacontext.CPUVal = cputotalusage;
                datacontext.RAMVal = ramusage;

                if (disk1 != null)
                {
                    disk1usage = ((disk1.TotalSize - disk1.TotalFreeSpace) / (double)disk1.TotalSize) * 100;
                    disk1usage = Math.Round(disk1usage, 1);
                    datacontext.Drive1Val = disk1usage;
                }

                if (disk2 != null)
                {
                    disk2usage = ((disk2.TotalSize - disk2.TotalFreeSpace) / (double)disk2.TotalSize) * 100;
                    disk2usage = Math.Round(disk2usage, 1);
                    datacontext.Drive2Val = disk2usage;
                }

                Thread.Sleep(1000);
            }
        }
    }
}
