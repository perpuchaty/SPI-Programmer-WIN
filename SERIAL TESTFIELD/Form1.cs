using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static Nito.HashAlgorithms.CRC32;
using Nito.HashAlgorithms;
using System.Runtime.Remoting.Contexts;
using System.Runtime.Remoting.Messaging;
using System.Collections;
using System.Security.Cryptography;
using System.Threading;



namespace SERIAL_TESTFIELD
{
    public partial class Form1 : Form
    {
        private BackgroundWorker chipErase = new BackgroundWorker();
        private BackgroundWorker programChip = new BackgroundWorker();

        //file
        static SerialPort _serialPort;
        bool connection = false;
        byte[] crc32 = new byte[4];
        byte[] crc32Flash = new byte[4];
        byte[] flashhex;
        string filename;

        //flash
        int flashSize;
        
        //flags
        bool flag_chip_erase = false;
        bool flag_chip_erased = false;

        public Form1()
        {
            InitializeComponent();
            programChip.DoWork += new DoWorkEventHandler(programChip_DoWork);
            programChip.RunWorkerCompleted += new RunWorkerCompletedEventHandler(programChip_RunWorkerCompleted);
            programChip.ProgressChanged += new ProgressChangedEventHandler(programChip_ProgressChanged);
            programChip.WorkerReportsProgress = true;
            programChip.WorkerSupportsCancellation = true;

            chipErase.DoWork += new DoWorkEventHandler(chipErase_DoWork);
            chipErase.RunWorkerCompleted += new RunWorkerCompletedEventHandler(chipErase_RunWorkerCompleted);
            chipErase.ProgressChanged += new ProgressChangedEventHandler(chipErase_ProgressChanged);
            chipErase.WorkerReportsProgress = true;
            chipErase.WorkerSupportsCancellation = true;
        }



        private void button1_Click(object sender, EventArgs e)
        {
            if (connection == false)
            {
     
                _serialPort = new SerialPort();
                _serialPort.BaudRate = 115200;
                _serialPort.PortName = textBox1.Text;
                _serialPort.ReadTimeout = 500;
                _serialPort.WriteTimeout = 10000;
                _serialPort.Open();
                _serialPort.Write("D"); //Get device id and size of source chip
                System.Threading.Thread.Sleep(100);//Wait for answer little bit

                /*int bytes = _serialPort.BytesToRead;
                byte[] buffer = new byte[bytes];

                //todo
                _serialPort.Read(buffer, 0, bytes);//fill buffer with data
                string devid = "0x" + buffer[0].ToString("X2");
                flashSize = buffer[1];
                string size = "0x" + buffer[1].ToString("X2");

                labelVendorId.Text = devid;
                labelChipSize.Text = size;*/

                connection = true;
                button1.Text = "Disconnect";
                groupBox1.Enabled = true;
                groupBox2.Enabled = true;
                groupBox4.Enabled = true;
                button3.Enabled = true;
            }
            else
            {
                _serialPort.Close();
                button1.Text = "Connect";
                connection = false;
                groupBox1.Enabled = false;
                groupBox2.Enabled = false;
                groupBox4.Enabled = false;
                button3.Enabled = false;

            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        protected void programChip_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker sendingWorker = (BackgroundWorker)sender;//Capture the BackgroundWorker that fired the event
                                                                     //Open file
            FileStream flashImage = new FileStream(openFileDialog1.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            int numberOfSectors = (int)(flashImage.Length / 4096);
            progressBarTotalProgress.Invoke((MethodInvoker)delegate
            {
                progressBarTotalProgress.Maximum = numberOfSectors;
                progressBarTotalProgress.Value = 0;

            });
  
            var definition = new Definition
            {
                Initializer = 0xFFFFFFFF,
                TruncatedPolynomial = 0x04C11DB7,
                FinalXorValue = 0x00000000,
                ReverseResultBeforeFinalXor = false,
                ReverseDataBytes = false
            };

            for (int i = 0; i < numberOfSectors; i++)
            {
                //Create buffer
                byte[] sector = new byte[4096];
                //Set position in the flash image
                flashImage.Position = i * 4096;
                //Copy one 1kb to buffer
                flashImage.Read(sector, 0, 4096);
                //Send to serial
                _serialPort.Write(sector, 0, 4096);

                MemoryStream stream = new MemoryStream(sector);
                StreamReader reader = new StreamReader(stream);
                string text = reader.ReadToEnd();
                var whow = new Nito.HashAlgorithms.CRC32(definition);
                crc32 = whow.ComputeHash(sector);

                //Show hash
                label1.Invoke((MethodInvoker)delegate
                {
                    label1.Text = "0x" + crc32[3].ToString("X2") + crc32[2].ToString("X2") + crc32[1].ToString("X2") + crc32[0].ToString("X2");

                });
                label2.Invoke((MethodInvoker)delegate
                {
                    label2.Text = i + "/" + Convert.ToString(numberOfSectors);
                });

                //Send hash in 4 bytes, total frame has 4100bytes
                _serialPort.Write(crc32, 0, 4);

                while (_serialPort.BytesToRead != 2) ;

                int j = 0;
                string buffer = _serialPort.ReadLine();
                if (buffer == "!") //Checksum fine, send next sector
                {
                    j = 1;
                }
                if (buffer == "?")//Checksum failed, send frame once more
                {
                    j = 1;
                    i = i - 1;      //This will cause to send the same frame one more time
                }
                sendingWorker.ReportProgress(i);//Report our progress to the main thread
            }
            _serialPort.Write(crc32Flash, 0, 4);

        }

        protected void programChip_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            button1.Enabled = true;
            button3.Enabled = true;
        }

        protected void programChip_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBarTotalProgress.Value = e.ProgressPercentage;
        }


        protected void chipErase_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker sendingWorker = (BackgroundWorker)sender;
            progressBarTotalProgress.Invoke((MethodInvoker)delegate
            {
                progressBarTotalProgress.Maximum = 100;
            });
            labelStatus.Invoke((MethodInvoker)delegate
            {
                labelStatus.Text = "Chip erase";
            });
            _serialPort.Write("C");

                for (int i = 0; i < 100; i++) //Executed every second. 100 seconds is the timeout for chip erase.
                {
                    System.Threading.Thread.Sleep(1000);
                    try
                    {
                        string buffer = _serialPort.ReadLine();
                        if (buffer == "DONE\r")
                        {
                            _serialPort.WriteLine(filename);
                        System.Threading.Thread.Sleep(10);
                        _serialPort.Write("!");
                            i = 100;
                        System.Threading.Thread.Sleep(100);
                        flag_chip_erased = true;
                        }
                    }
                    catch
                    {

                    }
                    sendingWorker.ReportProgress(i);//Report our progress to the main thread
                }
            
        }
        protected void chipErase_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
                programChip.RunWorkerAsync();           //Chip is erased, let's program it! todo

        }
        protected void chipErase_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBarTotalProgress.Value = e.ProgressPercentage;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            button3.Enabled = false;
              chipErase.RunWorkerAsync();   
        }

        private void button3_Click(object sender, EventArgs e)
        {
              openFileDialog1.Title = "Select file to flash";
              openFileDialog1.ShowDialog();

            FileStream flashImage = new FileStream(openFileDialog1.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            filename= Path.GetFileName(openFileDialog1.FileName);
            labelFilename.Text = Path.GetFileName(openFileDialog1.FileName);
            _serialPort.WriteLine(filename);
            Array.Resize(ref flashhex, (int)flashImage.Length);
            flashhex = flashImage.ReadAllBytes();
            int numberOfSectors = (int)(flashImage.Length / 4096);
            labelFileSize.Text = Convert.ToString(flashImage.Length);
            labelNumberOfSectors.Text = Convert.ToString(numberOfSectors);
            var definition = new Definition
            {
                Initializer = 0xFFFFFFFF,
                TruncatedPolynomial = 0x04C11DB7,
                FinalXorValue = 0x00000000,
                ReverseResultBeforeFinalXor = false,
                ReverseDataBytes = false
            };

            //Calculate CRC32 for entire flash
            MemoryStream stream_crc = new MemoryStream(flashhex);
            StreamReader reader_crc = new StreamReader(stream_crc);
            string text_crc = reader_crc.ReadToEnd();
            var whow_crc = new Nito.HashAlgorithms.CRC32(definition);
            crc32Flash = whow_crc.ComputeHash(flashhex);
            checksum.Text = "0x" + crc32Flash[3].ToString("X2") + crc32Flash[2].ToString("X2") + crc32Flash[1].ToString("X2") + crc32Flash[0].ToString("X2");
            button2.Enabled = true;
            //Calculate CRC32 for entire flash
        }
    }
}
public static class StreamExtensions
{
    public static byte[] ReadAllBytes(this Stream instream)
    {
        if (instream is MemoryStream)
            return ((MemoryStream)instream).ToArray();

        using (var memoryStream = new MemoryStream())
        {
            instream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
}