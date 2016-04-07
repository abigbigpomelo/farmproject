using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.IO.Ports;
using ZedGraph;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Configuration;

namespace SerialCommunicate
{
    public partial class Form1 : Form
    {
        byte[] senddata1 = new byte[7];
        double tcure;
        string wendu, shidu;

        PointPairList list1 = new PointPairList();
        PointPairList list2 = new PointPairList();
        PointPairList list3 = new PointPairList();
        PointPairList list4 = new PointPairList();
        LineItem myCurve;
        public Form1()
        {
            InitializeComponent();
            System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = false;
            //检查是否含有串口
            string[] str = SerialPort.GetPortNames();
            if (str == null)
            {
                MessageBox.Show("本机没有串口！", "Error");
                return;
            }
            
            //添加串口项目
            foreach (string s in System.IO.Ports.SerialPort.GetPortNames())
            {//获取有多少个COM口
                //System.Diagnostics.Debug.WriteLine(s);
                comboBox1.Items.Add(s);
            }
            comboBox1.SelectedIndex = 0;
        }


        private void button1_Click(object sender, EventArgs e)
        {
            senddata1[0] = 0xFD;
            senddata1[1] = 0x03;
            senddata1[2] = 0x28;
            senddata1[3] = 0x7B;
            senddata1[4] = 0xFF;
            senddata1[5] = 0xAA;
            senddata1[6] = 0xFF;

            button1.Enabled = false;//打开串口按钮不可用
            button2.Enabled = true;//关闭串口
            try
            {
                string serialName = comboBox1.SelectedItem.ToString();
                serialPort1.PortName = serialName;
                serialPort1.BaudRate = 38400;
                serialPort1.Open();
                timer4.Start();
                timer1.Start();
                timer2.Start();
            }
            catch {
                MessageBox.Show("端口错误,请检查串口", "错误");
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
            serialPort1.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);//必须手动添加事件处理程序

            this.zedGraphControl1.GraphPane.Title.Text = "实时数据波形";
            this.zedGraphControl1.GraphPane.XAxis.Title.Text = "时间";
            this.zedGraphControl1.GraphPane.YAxis.Title.Text = "温度值";
            this.zedGraphControl1.GraphPane.XAxis.MajorGrid.IsVisible = true;
            this.zedGraphControl1.GraphPane.YAxis.MajorGrid.IsVisible = true;
            this.zedGraphControl1.GraphPane.XAxis.Type = ZedGraph.AxisType.DateAsOrdinal;

            DateTime dt = DateTime.Now;
            myCurve = zedGraphControl1.GraphPane.AddCurve("温度", list1, Color.Blue, SymbolType.None);
            this.zedGraphControl1.AxisChange();
            this.zedGraphControl1.Refresh();
        }
        private List<byte> readedDataBuffer = new List<byte>();
        private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)//串口数据接收事件
        {
            lock (readedDataBuffer)
            {
                try
                {
                    if (serialPort1.BytesToRead >= 0)
                    {
                        byte[] data = new byte[serialPort1.BytesToRead];
                        int count = serialPort1.Read(data, 0, data.Length);

                        for (int i = 0; i < count; ++i)
                            readedDataBuffer.Add(data[i]);
                    }
                }
                catch { }
            }
            if (readedDataBuffer.Count >= 20)
            {
                int removeCount = 0;
                for (int i = 0; i < readedDataBuffer.Count - 18; ++i)
                {
                        // 14 7B节点
                        if (readedDataBuffer[i] == 0xFD & readedDataBuffer[i + 4] == 0x28 & readedDataBuffer[i + 5] == 0x7B)
                        {
                            byte[] data = new byte[20];

                            /*节点3*/
                            int data31, data32;
                            double v3, T3;
                            data31 = readedDataBuffer[i + 12];//高八位
                            data32 = readedDataBuffer[i + 13];//低八位
                            v3 = (data31 * 256 + data32);//电压值
                            T3 = -39.66 + 0.01 * v3 + 0.026;
                            tcure = T3;
                            wendu = T3.ToString();
                    }
                    removeCount++;
                }

                if (readedDataBuffer.Count >= removeCount)
                    readedDataBuffer.RemoveRange(0, removeCount);
            }

        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            //sqlite 数据库
            string uploadDate = System.DateTime.Now.ToShortDateString().ToString();
            string uploadTime = System.DateTime.Now.ToLongTimeString().ToString();
            SQLiteConnection conn = null;

            string dbPath = "Data Source =" + "C:/sqlite" + "/数据测试.db";
            conn = new SQLiteConnection(dbPath);//创建数据库实例，指定文件位置  
            conn.Open();//打开数据库，若文件不存在会自动创建  

            string sql = "CREATE TABLE IF NOT EXISTS '" + "温度" + uploadDate + "' (日期 varchar(6), 时间 varchar(6), 温度 varchar(6));";//建表语句  
            SQLiteCommand cmdCreateTable = new SQLiteCommand(sql, conn);
            cmdCreateTable.ExecuteNonQuery();//如果表不存在，创建数据表  

            SQLiteCommand cmdInsert = new SQLiteCommand(conn);
            cmdInsert.CommandText = "INSERT INTO '" + "温度" + uploadDate + "'  VALUES('" + uploadDate + "','" + uploadTime + "','" + wendu + "')";//插入几条数据  
            cmdInsert.ExecuteNonQuery();

            conn.Close();
        }

        private void label22_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            button1.Enabled = true;
            button2.Enabled = false;
            try
            {
                timer1.Stop();
                timer4.Stop();
                timer2.Stop();
                serialPort1.Close();
            }
            catch (Exception err)//一般情况下关闭串口不会出错，所以不需要加处理程序
            {
                MessageBox.Show("关闭数据出现错误", "错误提示");
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            serialPort1.Write(senddata1, 0, 7);
        }

        private void timer4_Tick(object sender, EventArgs e)
        {
            zedGraphControl1.GraphPane.XAxis.Scale.MaxAuto = true;
            this.zedGraphControl1.GraphPane.XAxis.Title.Text = "当前温度：" + wendu + " ℃" + "     时间: " + System.DateTime.Now;
            Double x = (Double)new XDate(DateTime.Now);
            textBox1.AppendText(DateTime.Now.ToLongTimeString() + '\n');

            double y = tcure;
            list1.Add(x, y);

            if (list1.Count >= 180)
            {
                list1.RemoveAt(0);
            }
            if (list2.Count >= 180)
            {
                list2.RemoveAt(0);
            }
            if (list3.Count >= 180)
            {
                list3.RemoveAt(0);
            }
            if (list4.Count >= 180)
            {
                list4.RemoveAt(0);
            }

            this.zedGraphControl1.AxisChange();

            this.zedGraphControl1.Refresh();
        }
    }
}
