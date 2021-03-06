﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using System.Windows.Forms ;
using System.Data.Sql;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using PacketViewerLogViewer;

namespace PacketViewerLogViewer.Packets
{
    public enum PacketLogTypes { Unknown, Outgoing, Incoming }
    public enum FilterType { Off, HidePackets, ShowPackets, AllowNone };
    public enum PacketLogFileFormats { Unknown = 0, WindowerPacketViewer = 1, AshitaPacketeer = 2, PacketDB = 3 }

    public static class String6BitEncodeKeys
    {
        public static char[] Item = new char[0x40] {
        //   0    1    2    3    4    5    6    7    8    9    A    B    C    D    E    F
           '\0', '0', '1', '2', '3', '4', '5', '6', '7', '9', '8', 'A', 'B', 'C', 'D', 'E', // 0x00
            'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', // 0x10
            'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', // 0x20
            'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '\0' // 0x30
        };
        public static char[] Linkshell = new char[0x40] {
        //   0    1    2    3    4    5    6    7    8    9    A    B    C    D    E    F
           '\0', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', // 0x00
            'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D', 'E', // 0x10
            'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', // 0x20
            'V', 'W', 'X', 'Y', 'Z', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', '\0' // 0x30
        };
    }

    public class PacketData
    {
        public List<String> RawText { get; set; }
        public string HeaderText { get; set; }
        public string OriginalHeaderText { get; set; }
        public List<byte> RawBytes { get; set; }
        public PacketLogTypes PacketLogType { get; set; }
        public UInt16 PacketID { get; set; }
        public UInt16 PacketDataSize { get; set; }
        public UInt16 PacketSync { get; set; }
        public DateTime TimeStamp { get; set; }
        public DateTime VirtualTimeStamp { get; set; }
        public string OriginalTimeString { get; set; }
        public int capturedZoneId { get; set; }

        // Source: https://docs.microsoft.com/en-us/dotnet/api/system.datetime.parse?view=netframework-4.7.2#System_DateTime_Parse_System_String_System_IFormatProvider_System_Globalization_DateTimeStyles_
        // Assume a date and time string formatted for the fr-FR culture is the local 
        // time and convert it to UTC.
        // dateString = "2008-03-01 10:00";
        private CultureInfo cultureForDateTimeParse = CultureInfo.CreateSpecificCulture("fr-FR"); // French seems to best match for what we need here
        private DateTimeStyles stylesForDateTimeParse = DateTimeStyles.AssumeLocal;

        public PacketData()
        {
            RawText = new List<string>();
            HeaderText = "Unknown Header";
            OriginalHeaderText = "";
            RawBytes = new List<byte>();
            PacketLogType = PacketLogTypes.Unknown;
            PacketID = 0x000 ;
            PacketDataSize = 0x0000 ;
            PacketSync = 0x0000 ;
            TimeStamp = new DateTime(0);
            VirtualTimeStamp = new DateTime(0);
            OriginalTimeString = "";
            capturedZoneId = 0;
        }

        ~PacketData()
        {
            RawText.Clear();
            RawBytes = null ;
        }

        public int AddRawLineAsBytes(string s)
        {
            /* Example:
            //        1         2         3         4         5         6         7         8         9
            //34567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890

            [2018-05-16 18:11:35] Outgoing packet 0x015:
                    |  0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F      | 0123456789ABCDEF
                -----------------------------------------------------  ----------------------
                  0 | 15 10 9E 00 CF 50 A0 C3 04 0E 1C C2 46 BF 33 43    0 | .....P......F.3C
                  1 | 00 00 02 00 5D 00 00 00 49 97 B8 69 00 00 00 00    1 | ....]...I..i....

            // 1         2         3         4         5         6         7         8         9
            // 123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890
            // 5 | 00 00 00 00 -- -- -- -- -- -- -- -- -- -- -- --    5 | ....------------
            */

            // if (s.Length < 81)
            if (s.Length < 57)
            {
                // Doesn't look like a correct format
                return 0;
            }

            int c = 0;
            for(int i = 0 ; i <= 0xf ; i++)
            {
                var h = s.Substring(10 + (i * 3), 2);
                if (h != "--")
                {
                    try
                    {
                        byte b = byte.Parse(h, System.Globalization.NumberStyles.HexNumber);
                        RawBytes.Add(b);
                    }
                    catch { }
                    c++;
                }
            }
            return c;
        }

        public int AddRawPacketeerLineAsBytes(string s)
        {
            /* Example:
            // 1         2         3         4         5         6         7         8         9
            // 123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890
            // [C->S] Id: 001A | Size: 28
            // 1A 0E ED 24 D5 10 10 01 D5 00 00 00 00 00 00 00  ..í$Õ...Õ.......
            */

            if (s.Length < 51)
            {
                // Doesn't look like a correct format
                return 0;
            }

            int c = 0;
            for (int i = 0; i <= 0xf; i++)
            {
                var h = s.Substring(11 + (i * 3), 2);
                // If this fails, we're probably at the end of the packet
                // Unlike windower, Packeteer doesn't add dashes for the blanks
                if ((h != "--") && (h != "  ") && (h != " "))
                {
                    if (!byte.TryParse("0x" + h, out byte b))
                        break;
                    RawBytes.Add(b);
                    c++;
                }
            }
            return c;
        }

        public int AddRawHexDataAsBytes(string hexData)
        {
            int res = 0;
            try
            {
                RawBytes.Clear();
                string dataLine = hexData.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");
                for (int i = 0; i < (dataLine.Length - 1); i += 2)
                {
                    string num = dataLine.Substring(i, 2);
                    byte b = byte.Parse(num, System.Globalization.NumberStyles.HexNumber);
                    RawBytes.Add(b);
                    res++;
                }
                /*
                string[] nums = hexData.Split(' ');
                foreach(string num in nums)
                {
                    byte b = byte.Parse(num, System.Globalization.NumberStyles.HexNumber);
                    RawBytes.Add(b);
                    res++;
                }
                */
            }
            catch
            {
                //
            }
            return res;
        }

        public string PrintRawBytesAsHex()
        {
            const int ValuesPerRow = 16;
            string res = "";
            res += "   |  0  1  2  3   4  5  6  7   8  9  A  B   C  D  E  F\r\n";
            res += "---+----------------------------------------------------\r\n";
            int lineNumber = 0;
            for(int i = 0; i < RawBytes.Count; i++)
            {
                if ((i % ValuesPerRow) == 0)
                    res += lineNumber.ToString("X2") + " | " ;

                res += RawBytes[i].ToString("X2");

                if ((i % ValuesPerRow) == (ValuesPerRow - 1))
                {
                    res += "\r\n" ;
                    lineNumber++;
                }
                else
                {
                    res += " " ;
                    if ((i % 4) == 3)
                        res += " " ; // Extra space every 4 bytes
                }
            }
            return res;
        }

        public byte GetByteAtPos(int pos)
        {
            if (pos > (RawBytes.Count - 1))
                return 0;
            return RawBytes[pos];
        }

        public bool GetBitAtPos(int pos, int bit)
        {
            if ((pos > (RawBytes.Count - 1)) || ((bit < 0) || (bit > 7)))
                return false;
            byte b = RawBytes[pos];
            byte bitmask = (byte)(0x01 << bit);
            return ((b & bitmask) != 0);
        }

        public UInt16 GetUInt16AtPos(int pos)
        {
            if (pos > (RawBytes.Count - 2))
                return 0;
            return BitConverter.ToUInt16(RawBytes.GetRange(pos, 2).ToArray(), 0);
        }

        public Int16 GetInt16AtPos(int pos)
        {
            if (pos > (RawBytes.Count - 2))
                return 0;
            return BitConverter.ToInt16(RawBytes.GetRange(pos, 2).ToArray(), 0);
        }

        public UInt32 GetUInt32AtPos(int pos)
        {
            if (pos > (RawBytes.Count - 4))
                return 0;
            return BitConverter.ToUInt32(RawBytes.GetRange(pos, 4).ToArray(), 0);
        }

        public Int32 GetInt32AtPos(int pos)
        {
            if (pos > (RawBytes.Count - 4))
                return 0;
            return BitConverter.ToInt32(RawBytes.GetRange(pos, 4).ToArray(), 0);
        }

        public UInt64 GetUInt64AtPos(int pos)
        {
            if (pos > (RawBytes.Count - 8))
                return 0;
            return BitConverter.ToUInt64(RawBytes.GetRange(pos, 8).ToArray(), 0);
        }

        public Int64 GetInt64AtPos(int pos)
        {
            if (pos > (RawBytes.Count - 8))
                return 0;
            return BitConverter.ToInt64(RawBytes.GetRange(pos, 8).ToArray(), 0);
        }


        public string GetTimeStampAtPos(int pos)
        {
            string res = "???";
            if (pos > (RawBytes.Count - 4))
                return res;

            try
            {
                UInt32 DT = GetUInt32AtPos(pos);
                // Unix timestamp is seconds past epoch
                System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
                dtDateTime = dtDateTime.AddSeconds(DT).ToLocalTime();
                res = dtDateTime.ToLongTimeString();
            }
            catch
            {
                res = "ERROR";
            }
            return res;
        }

        public string GetStringAtPos(int pos, int maxSize = -1)
        {
            string res = string.Empty;
            int i = 0;
            while (((i+pos) < RawBytes.Count) && (RawBytes[pos+i] != 0) && ((maxSize == -1) || (res.Length < maxSize)))
            {
                res += (char)RawBytes[pos+i];
                i++;
            }
            return res;
        }

        public string GetDataAtPos(int pos, int size)
        {
            string res = "";
            int i = 0;
            while ( ((i+pos) < RawBytes.Count) && (i < size) && (i < 256) )
            {
                res += RawBytes[i + pos].ToString("X2") + " ";
                i++;
            }
            return res;
        }

        public string GetIP4AtPos(int pos)
        {
            if (pos > RawBytes.Count - 4)
                return "";
            return RawBytes[pos + 0].ToString() + "." + RawBytes[pos + 1].ToString() + "." + RawBytes[pos + 2].ToString() + "." + RawBytes[pos + 3].ToString();
        }

        public string GetJobFlagsAtPos(int pos)
        {
            string res = "";
            if (pos > (RawBytes.Count - 4))
                return res;
            UInt32 Flags = GetUInt32AtPos(pos);
            for(uint BitShiftCount = 0; BitShiftCount < 32; BitShiftCount++)
            {
                if ((Flags & 0x00000001) == 1)
                {
                    if (res != "")
                        res += " ";

                    if (BitShiftCount == 0)
                    {
                        res += "SubJob";
                    }
                    else
                    {
                        var JobName = DataLookups.NLU(DataLookups.LU_Job).GetValue(BitShiftCount);
                        if (JobName == "")
                            JobName = "[Bit" + BitShiftCount.ToString() + "]";
                        res += JobName;
                    }

                }
                Flags = Flags >> 1;
            }

            return res;
        }

        public Int64 GetBitsAtPos(int pos, int BitOffset, int BitsSize)
        {
            Int64 res = 0;
            int P = pos;
            int B = BitOffset;
            int RestBits = BitsSize;
            // Minimum 1 bit
            if (RestBits < 1)
                RestBits = 1;
            Int64 Mask = 1;
            while (RestBits > 0)
            {
                while (B >= 8)
                {
                    B -= 8;
                    P++;
                }
                // Add Mask Value if Bit is set
                if (GetBitAtPos(P, B))
                    res += Mask;
                RestBits--;
                Mask = Mask << 1;
                B++;
            }
            return res;
        }

        public Int64 GetBitsAtBitPos(int BitOffset, int BitsSize)
        {
            return GetBitsAtPos(BitOffset / 8, BitOffset % 8, BitsSize);
        }

        public string GetPackedString16AtPos(int pos, char[] Encoded6BitKey)
        {
            string res = "";
            // Hex: B8 81 68 24  72 14 4F 10  54 0C 8F 00  00 00 00 00
            // Bit: 101110 00
            // 1000 0001
            // 01 101000
            // 001001 00
            // 0111 0010
            // 00 010100
            // 010011 11
            // 0001 0000
            // 01 010100
            // 000011 00
            // 1000 1111
            // 00 000000

            // PackedString: TheNightsWatch (with no spaces)
            // PackedNum: 2E 08 05 ...
            // 101110  T
            // 001000  h
            // 000101  e
            //

            // A_  6F F0    011011 11-1111 0000  =>  1B 3F 00  =>  A
            // B_  73 F0    011100 11-1111 0000  =>  1C 3F 00  =>  B
            // F_  83 F0    100000 11-1111 0000  =>  20 3F 00  =>  F

            // EncodeLSStr : Array [0..63] of Char = (
            // #0 ,'a','b','c','d','e','f','g',  'h','i','j','k','l','m','n','o', // $00
            // 'p','q','r','s','t','u','v','w',  'x','y','z','A','B','C','D','E', // $10
            // 'F','G','H','I','J','K','L','M',  'N','O','P','Q','R','S','T','U', // $20
            // 'V','W','X','Y','Z',' ',' ',' ',  ' ',' ',' ',' ',' ',' ',' ', #0  // $30
            //  0   1   2   3   4   5   6   7     8   9   A   B   C   D   E   F
            // );
            int Offset = 0;
            while ((Offset / 8) < 15)
            {
                byte encodedChar = 0;
                byte bitMask = 0b00100000;
                for (int bit = 0; bit < 6; bit++)
                {
                    bool isSet = GetBitAtPos(pos + ((Offset+bit) / 8), 7 - ((Offset+bit) % 8));
                    if (isSet)
                        encodedChar += bitMask;
                    bitMask >>= 1;
                }
                // GetBitsAtPos(pos + (Offset / 8), (Offset % 8), 6);
                if ((encodedChar >= Encoded6BitKey.Length) || (encodedChar < 0))
                    break;
                var c = Encoded6BitKey[encodedChar];
                if (c == 0)
                    break;
                res += c;
                Offset += 6;
            }
            return res;
        }

        public Single GetFloatAtPos(int pos)
        {
            if (pos > (RawBytes.Count - 4))
                return 0;
            return BitConverter.ToSingle(RawBytes.GetRange(pos, 4).ToArray(), 0);
        }

        public int FindByte(byte aByte)
        {
            return RawBytes.IndexOf(aByte);
        }

        public int FindUInt16(UInt16 aUInt16)
        {
            for(int i = 0; i < (RawBytes.Count - 2); i++)
            {
                if (GetUInt16AtPos(i) == aUInt16)
                    return i;
            }
            return -1;
        }

        public int FindUInt32(UInt32 aUInt32)
        {
            for (int i = 0; i < (RawBytes.Count - 4); i++)
            {
                if (GetUInt32AtPos(i) == aUInt32)
                    return i;
            }
            return -1;
        }

        public int FindUInt64(UInt64 aUInt64)
        {
            for (int i = 0; i < (RawBytes.Count - 8); i++)
            {
                if (GetUInt64AtPos(i) == aUInt64)
                    return i;
            }
            return -1;
        }

        public bool DateTimeParse(string s, out DateTime res)
        {
            res = new DateTime(0);
            if (s.Length != 19)
                return false;
            try
            {
                // 0         1
                // 01234567890123456789
                // 2018-05-16 18:11:35
                var yyyy = int.Parse(s.Substring(0, 4));
                var mm = int.Parse(s.Substring(5, 2));
                var dd = int.Parse(s.Substring(8, 2));
                var hh = int.Parse(s.Substring(11, 2));
                var nn = int.Parse(s.Substring(14, 2));
                var ss = int.Parse(s.Substring(17, 2));
                res = new DateTime(yyyy, mm, dd, hh, nn, ss);
                return true;
            }
            catch
            {
            }
            return false ;
        }

        public bool CompileData(PacketLogFileFormats plff)
        {
            if (RawBytes.Count < 4)
            {
                PacketID = 0xFFFF;
                PacketDataSize = 0;
                HeaderText = "Invalid Packet Size < 4";
                return false;
            }
            PacketID = (UInt16)( GetByteAtPos(0) + ((GetByteAtPos(1) & 0x01) * 0x100) );
            PacketDataSize = (UInt16)((GetByteAtPos(1) & 0xFE) * 2);
            PacketSync = (UInt16)(GetByteAtPos(2) + (GetByteAtPos(3) * 0x100));
            string TS = "";
            if (TimeStamp.Ticks > 0)
                TS = TimeStamp.ToString("HH:mm:ss");
            if (plff == PacketLogFileFormats.AshitaPacketeer)
            {
                // Packeteer doesn't have time info (yet)
                TimeStamp = new DateTime(0);
                OriginalTimeString = "0000-00-00 00:00";
            }
            if (plff == PacketLogFileFormats.WindowerPacketViewer)
            {
                // Try to determine timestamp from header
                var P1 = OriginalHeaderText.IndexOf('[');
                var P2 = OriginalHeaderText.IndexOf(']');
                if ((P1 >= 0) && (P2 >= 0) && (P2 > P1))
                {
                    OriginalTimeString = OriginalHeaderText.Substring(P1 + 1, P2 - P1 - 1);
                    if (OriginalTimeString.Length > 0)
                    {
                        try
                        {
                            // try quick-parse first
                            DateTime dt;
                            if (DateTimeParse(OriginalTimeString, out dt))
                            {
                                TimeStamp = dt;
                            }
                            else
                            {
                                TimeStamp = DateTime.Parse(OriginalTimeString, cultureForDateTimeParse, stylesForDateTimeParse);
                            }
                            TS = TimeStamp.ToString("HH:mm:ss");
                        }
                        catch (FormatException)
                        {
                            TimeStamp = new DateTime(0);
                            TS = "";
                            OriginalTimeString = "0000-00-00 00:00";
                        }
                    }
                }
            }
            VirtualTimeStamp = TimeStamp;
            if (TimeStamp.Ticks == 0)
                TS = "";

            string S = "";
            switch(PacketLogType)
            {
                case PacketLogTypes.Outgoing:
                    S = "OUT ";
                    break;
                case PacketLogTypes.Incoming:
                    S = "IN  ";
                    break;
                default:
                    S = "??? ";
                    break;
            }
            S = TS + " : " + S + "0x" + PacketID.ToString("X3") + " - ";
            HeaderText = S + DataLookups.PacketTypeToString(PacketLogType, PacketID);
            return true;
        }

    }

    public class PacketListFilter
    {
        public FilterType FilterOutType { get; set; }
        public List<UInt16> FilterOutList { get; set; }
        public FilterType FilterInType { get; set; }
        public List<UInt16> FilterInList { get; set; }

        public PacketListFilter()
        {
            FilterOutList = new List<UInt16>();
            FilterInList = new List<UInt16>();
            Clear();
        }

        public void Clear()
        {
            FilterOutType = FilterType.Off;
            FilterOutList.Clear();
            FilterInType = FilterType.Off;
            FilterInList.Clear();
        }

        public void CopyFrom(PacketListFilter aFilter)
        {
            FilterOutType = aFilter.FilterOutType;
            FilterInType = aFilter.FilterInType;
            FilterOutList.Clear();
            FilterOutList.AddRange(aFilter.FilterOutList);
            FilterInList.Clear();
            FilterInList.AddRange(aFilter.FilterInList);
        }

        public void AddOutFilterValueToList(UInt16 value)
        {
            if ((value > 0) && (FilterOutList.IndexOf(value) < 0))
                FilterOutList.Add(value);
        }

        public void AddInFilterValueToList(UInt16 value)
        {
            if ((value > 0) && (FilterInList.IndexOf(value) < 0))
                FilterInList.Add(value);
        }

        public bool LoadFromFile(string filename)
        {
            try
            {
                List<string> sl = File.ReadAllLines(filename).ToList();

                Clear();
                foreach(string line in sl)
                {
                    var fields = line.Split(';');
                    if (fields.Length <= 1)
                        continue;

                    var f0 = fields[0].ToLower();
                    var f1 = fields[1].ToLower();

                    switch (f0)
                    {
                        case "outtype":
                            switch(f1)
                            {
                                case "off":
                                    FilterOutType = FilterType.Off;
                                    break;
                                case "show":
                                    FilterOutType = FilterType.ShowPackets;
                                    break;
                                case "hide":
                                    FilterOutType = FilterType.HidePackets;
                                    break;
                                case "none":
                                    FilterOutType = FilterType.AllowNone;
                                    break;
                            }
                            break;
                        case "intype":
                            switch (f1)
                            {
                                case "off":
                                    FilterInType = FilterType.Off;
                                    break;
                                case "show":
                                    FilterInType = FilterType.ShowPackets;
                                    break;
                                case "hide":
                                    FilterInType = FilterType.HidePackets;
                                    break;
                                case "none":
                                    FilterInType = FilterType.AllowNone;
                                    break;
                            }
                            break;
                        case "out":
                            if (DataLookups.TryFieldParse(f1, out int nout))
                                AddOutFilterValueToList((UInt16)nout);
                            break;
                        case "in":
                            if (DataLookups.TryFieldParse(f1, out int nin))
                                AddInFilterValueToList((UInt16)nin);
                            break;
                    }


                }

            }
            catch (Exception x)
            {
                MessageBox.Show("Failed to load " + filename + "\r\nException: " + x.Message, "Load Filter Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        public bool SaveToFile(string filename)
        {
            List<string> sl = new List<string>();
            sl.Add("rem;original-file;" + Path.GetFileName(filename));
            switch(FilterOutType)
            {
                case FilterType.Off:
                    sl.Add("outtype;off");
                    break;
                case FilterType.ShowPackets:
                    sl.Add("outtype;show");
                    break;
                case FilterType.HidePackets:
                    sl.Add("outtype;hide");
                    break;
                case FilterType.AllowNone:
                    sl.Add("outtype;none");
                    break;
            }
            foreach(UInt16 i in FilterOutList)
            {
                sl.Add("out;0x"+ i.ToString("X3") + ";" + DataLookups.NLU(DataLookups.LU_PacketOut).GetValue(i));
            }

            switch (FilterInType)
            {
                case FilterType.Off:
                    sl.Add("intype;off");
                    break;
                case FilterType.ShowPackets:
                    sl.Add("intype;show");
                    break;
                case FilterType.HidePackets:
                    sl.Add("intype;hide");
                    break;
                case FilterType.AllowNone:
                    sl.Add("intype;none");
                    break;
            }
            foreach (UInt16 i in FilterInList)
            {
                sl.Add("in;0x" + i.ToString("X3") + ";" + DataLookups.NLU(DataLookups.LU_PacketIn).GetValue(i));
            }

            try
            {
                File.WriteAllLines(filename, sl);
            }
            catch (Exception x)
            {
                MessageBox.Show("Failed to save " + filename + "\r\nException: " + x.Message, "Save Filter Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }
    }

    public class PacketList
    {
        protected List<PacketData> PacketDataList { get; set; }

        public PacketListFilter Filter ;

        public PacketList()
        {
            PacketDataList = new List<PacketData>();
            Filter = new PacketListFilter();
        }

        ~PacketList()
        {
            Filter.Clear();
            Clear();
        }

        public void Clear()
        {
            PacketDataList.Clear();
        }

         public bool LoadFromFile(string fileName)
        {
            if (!File.Exists(fileName))
                return false;
            PacketLogTypes packetType = PacketLogTypes.Unknown;
            PacketLogFileFormats logType = PacketLogFileFormats.Unknown;
            var fn = fileName.ToLower();
            // first check out, then in (as "in" is also in "ougoing")
            if ((packetType == PacketLogTypes.Unknown) && (Path.GetFileNameWithoutExtension(fn).IndexOf("out") >= 0))
                packetType = PacketLogTypes.Outgoing;
            if ((packetType == PacketLogTypes.Unknown) && (Path.GetFileNameWithoutExtension(fn).IndexOf("in") >= 0))
                packetType = PacketLogTypes.Incoming;
            if ((packetType == PacketLogTypes.Unknown) && (fn.IndexOf("out") >= 0))
                packetType = PacketLogTypes.Outgoing;
            if ((packetType == PacketLogTypes.Unknown) && (fn.IndexOf("in") >= 0))
                packetType = PacketLogTypes.Incoming;

            if ((logType == PacketLogFileFormats.Unknown) && (Path.GetExtension(fn) == ".log"))
                logType = PacketLogFileFormats.WindowerPacketViewer;
            if ((logType == PacketLogFileFormats.Unknown) && (Path.GetExtension(fn) == ".txt"))
                logType = PacketLogFileFormats.AshitaPacketeer;
            if ((logType == PacketLogFileFormats.Unknown) && (Path.GetExtension(fn) == ".sqlite"))
                logType = PacketLogFileFormats.PacketDB;

            if ((logType == PacketLogFileFormats.WindowerPacketViewer) || (logType == PacketLogFileFormats.AshitaPacketeer))
            {
                List<string> sl = File.ReadAllLines(fileName).ToList();
                return LoadFromStringList(sl, logType, packetType);
            }
            else
            if (logType == PacketLogFileFormats.PacketDB)
            {
                return LoadFromSQLite3(fileName);
            }
            else
            {
                return false;
            }
        }

        public bool LoadFromStringList(List<string> FileData,PacketLogFileFormats logFileType , PacketLogTypes preferedType)
        {
            // Add dummy blank lines to fix a bug of ignoring last packet if isn't finished by a blank line
            FileData.Add("");

            // TODO: Loading Form
            Application.UseWaitCursor = true;

            using (LoadingForm loadform = new LoadingForm(MainForm.thisMainForm))
            {
                try
                {
                    loadform.Text = "Loading text log file";
                    loadform.Show();
                    loadform.pb.Minimum = 0;
                    loadform.pb.Maximum = FileData.Count ;
                    loadform.pb.Step = 1000;

                    PacketData PD = null;
                    bool IsUndefinedPacketType = true;
                    bool AskForPacketType = true;

                    int c = 0;
                    foreach(string s in FileData)
                    {
                        string sLower = s.ToLower();
                        if ((s != "") && (PD == null))
                        {
                            // Begin building a new packet
                            PD = new PacketData();
                            if (sLower.IndexOf("incoming") >= 0)
                            {
                                PD.PacketLogType = PacketLogTypes.Incoming;
                                IsUndefinedPacketType = false;
                                logFileType = PacketLogFileFormats.WindowerPacketViewer;
                            }
                            else
                            if (sLower.IndexOf("outgoing") >= 0)
                            {
                                PD.PacketLogType = PacketLogTypes.Outgoing;
                                IsUndefinedPacketType = false;
                                logFileType = PacketLogFileFormats.WindowerPacketViewer;
                            }
                            else
                            if (sLower.IndexOf("[s->c]") >= 0)
                            {
                                PD.PacketLogType = PacketLogTypes.Incoming;
                                IsUndefinedPacketType = false;
                                logFileType = PacketLogFileFormats.AshitaPacketeer;
                            }
                            else
                            if (sLower.IndexOf("[c->s]") >= 0)
                            {
                                PD.PacketLogType = PacketLogTypes.Outgoing;
                                IsUndefinedPacketType = false;
                                logFileType = PacketLogFileFormats.AshitaPacketeer;
                            }
                            else
                            {
                                PD.PacketLogType = preferedType;
                            }

                            if (
                                // Not a comment or empty line
                                ((s != "") && (!s.StartsWith("--"))) &&
                                // Unknown packet and we need to know ?
                                (IsUndefinedPacketType && AskForPacketType && (PD.PacketLogType == PacketLogTypes.Unknown))
                               )
                            {
                                AskForPacketType = false;
                                // Ask for type
                                var askDlgRes = DialogResult.Cancel;
                                using (PacketTypeSelectForm askDlg = new PacketTypeSelectForm())
                                {
                                    askDlg.lHeaderData.Text = s;
                                    askDlgRes = askDlg.ShowDialog();
                                    //var askDlgStr = "Unable to indentify the packet type.\r\nDo you want to assign a default type ?\r\n\r\nPress YES for Incomming\r\n\r\nPress NO for outgoing\r\n\r\nPress Cancel to keep it undefined\r\n\r\nLineData:\r\n\r\n" + s.Substring(0, Math.Min(s.Length, 100)) + " ...";
                                    //MessageBox.Show(askDlgStr, "Packet Type ?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                                }
                                if (askDlgRes == DialogResult.Yes)
                                {
                                    preferedType = PacketLogTypes.Incoming ;
                                    IsUndefinedPacketType = false;
                                    PD.PacketLogType = preferedType ;
                                }
                                else
                                if (askDlgRes == DialogResult.No)
                                {
                                    preferedType = PacketLogTypes.Outgoing;
                                    IsUndefinedPacketType = false;
                                    PD.PacketLogType = preferedType;
                                }
                            }

                            PD.RawText.Add(s);
                            PD.HeaderText = s;
                            PD.OriginalHeaderText = s;

                            if (logFileType == PacketLogFileFormats.Unknown)
                            {
                                // Assume the pasted data is just raw hex bytes
                                PD.HeaderText = "Clipboard";
                                PD.OriginalHeaderText = "Clipboard Data";
                                PD.AddRawHexDataAsBytes(s);
                            }

                        } // end start new packet
                        else
                        if ((s != "") && (PD != null))
                        {
                            // Add line of data
                            PD.RawText.Add(s);
                            // Actual packet data starts at the 3rd line after the header
                            if ((logFileType != PacketLogFileFormats.AshitaPacketeer) && (PD.RawText.Count > 3))
                            {
                                PD.AddRawLineAsBytes(s);
                            }
                            else
                            if ((logFileType == PacketLogFileFormats.AshitaPacketeer) && (PD.RawText.Count > 1))
                            {
                                PD.AddRawPacketeerLineAsBytes(s);
                            }
                            else
                            if (logFileType == PacketLogFileFormats.Unknown)
                            {
                                // Assume the pasted data is just raw hex bytes
                                PD.AddRawHexDataAsBytes(s);
                            }
                        }
                        else
                        if ((s == "") && (PD != null))
                        {
                            // Close this packet and add it to list
                            if (PD.CompileData(logFileType))
                            {
                                PacketDataList.Add(PD);
                            }
                            else
                            {
                                // Invalid data
                            }
                            PD = null;
                        }
                        else
                        if ((s == "") && (PD == null))
                        {
                            // Blank line
                        }
                        else
                        if (s.StartsWith("--") && (PD != null))
                        {
                            // Comment
                        }
                        else
                        {
                            // ERROR, this should not be possible in a valid file, but just ignore it
                        }

                        c++;
                        if ((c % 1000) == 0)
                        {
                            loadform.pb.PerformStep();
                            loadform.pb.Refresh();
                        }
                    } // end foreach datafile line

                }
                catch
                {
                    Application.UseWaitCursor = false;
                    return false;
                }
            }
            Application.UseWaitCursor = false;
            return true;
        }

        public bool LoadFromSQLite3(string sqliteFileName)
        {
            int c = 0;
            using (LoadingForm loadform = new LoadingForm(MainForm.thisMainForm))
            {
                try
                {
                    loadform.Text = "Loading sqlite log file";
                    loadform.Show();
                    loadform.pb.Minimum = 0;
                    loadform.pb.Maximum = 100000;
                    loadform.pb.Step = 100;

                    using (SqliteConnection sqlConnection = new SqliteConnection("Data Source=" + sqliteFileName))
                    {
                        sqlConnection.Open();

                        string sql = "SELECT * FROM `packets` ORDER by PACKET_ID ASC";

                        SqliteCommand command = new SqliteCommand(sql, sqlConnection);
                        SqliteDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            PacketData pd = new PacketData();
                            pd.TimeStamp = reader.GetDateTime(reader.GetOrdinal("RECEIVED_DT"));
                            pd.VirtualTimeStamp = pd.TimeStamp;
                            var dir = reader.GetInt16(reader.GetOrdinal("DIRECTION")); // 0 = in ; 1 = out
                            switch (dir)
                            {
                                case 0:
                                    pd.PacketLogType = PacketLogTypes.Incoming;
                                    break;
                                case 1:
                                    pd.PacketLogType = PacketLogTypes.Outgoing;
                                    break;
                                default:
                                    pd.PacketLogType = PacketLogTypes.Unknown;
                                    break;
                            }
                            pd.PacketID = (UInt16)reader.GetInt32(reader.GetOrdinal("PACKET_TYPE"));
                            var pData = reader.GetString(reader.GetOrdinal("PACKET_DATA"));
                            pd.RawText.Add(pData);
                            pd.AddRawHexDataAsBytes(pData);
                            pd.capturedZoneId = reader.GetInt16(reader.GetOrdinal("ZONE_ID"));

                            pd.OriginalHeaderText = "PACKET_ID " + reader.GetInt64(reader.GetOrdinal("PACKET_ID")) + " , DIR " + dir.ToString() + " , TYPE " + pd.PacketID.ToString();
                            pd.OriginalTimeString = "";

                            if (pd.CompileData(PacketLogFileFormats.PacketDB))
                                PacketDataList.Add(pd);

                            c++;
                            if ((c % 100) == 0)
                            {
                                loadform.pb.PerformStep();
                                if (loadform.pb.Value >= loadform.pb.Maximum)
                                    loadform.pb.Value = loadform.pb.Minimum;
                                loadform.pb.Refresh();
                            }
                        }

                    }
                }
                catch (Exception x)
                {
                    MessageBox.Show("Exception: " + x.Message, "Exception loading SQLite file", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            return true;

        }

        public int Count()
        {
            return PacketDataList.Count;
        }

        public PacketData GetPacket(int index)
        {
            if ((index >= 0) && (index < PacketDataList.Count))
                return PacketDataList[index];
            if (PacketDataList.Count > 0)
                return PacketDataList[0];
            else
                return null;
        }

        public int CopyFrom(PacketList Original)
        {
            int c = 0;
            Clear();
            foreach(PacketData pd in Original.PacketDataList)
            {
                PacketDataList.Add(pd);
                c++;
            }
            return c;
        }

        protected bool DoIShowThis(UInt16 PacketID, FilterType FT, List<UInt16> FL)
        {
            switch (FT)
            { 
                case FilterType.AllowNone:
                    return false;
                case FilterType.ShowPackets:
                    return FL.Contains(PacketID);
                case FilterType.HidePackets:
                    return !FL.Contains(PacketID);
                case FilterType.Off:
                default:
                    return true;
            }
        }

        protected bool DoIShowThis(PacketData PD)
        {
            if ((PD.PacketLogType == PacketLogTypes.Incoming) && (Filter.FilterInType != FilterType.Off))
                return DoIShowThis(PD.PacketID, Filter.FilterInType, Filter.FilterInList);
            if ((PD.PacketLogType == PacketLogTypes.Outgoing) && (Filter.FilterOutType != FilterType.Off))
                return DoIShowThis(PD.PacketID, Filter.FilterOutType, Filter.FilterOutList);
            return true;
        }

        public int FilterFrom(PacketList Original)
        {
            int c = 0;
            Clear();
            foreach (PacketData pd in Original.PacketDataList)
            {
                if (DoIShowThis(pd))
                {
                    PacketDataList.Add(pd);
                    c++;
                }
            }
            return c;
        }

        public void BuildVirtualTimeStamps()
        {
            // Need a minimum of 3 packets to be able to have effect
            if (PacketDataList.Count <= 1)
                return;

            int i = 0;
            float divider = 0.0f;
            DateTime FirstOfGroupTime = GetPacket(0).TimeStamp;
            int FirstOfGroupIndex = 0;
            DateTime LastTimeStamp = FirstOfGroupTime;

            while (i < PacketDataList.Count)
            {
                PacketData thisPacket = GetPacket(i);
                if (thisPacket.TimeStamp == LastTimeStamp)
                {
                    // Same packet Group
                    divider += 1.0f;
                }
                if ((thisPacket.TimeStamp != LastTimeStamp) || (i >= PacketDataList.Count))
                {
                    // Last packet of the group
                    for(int n = 1; n < (divider - 1); n++)
                    {
                        TimeSpan stepTime = new TimeSpan(0, 0, 0, 0, (1000 / (int)Math.Round(divider * n)));
                        GetPacket(FirstOfGroupIndex + n).VirtualTimeStamp = FirstOfGroupTime + stepTime;
                    }

                    if (i < (PacketDataList.Count - 1))
                    {
                        // If not the last one
                        FirstOfGroupIndex = i + 1;
                        FirstOfGroupTime = GetPacket(i + 1).TimeStamp;
                    }
                }
                LastTimeStamp = thisPacket.TimeStamp;
                i++;
            }

        }

    } // End PacketList

    class PacketTabPage: System.Windows.Forms.TabPage
    {
        public PacketList PLLoaded; // File Loaded
        public PacketList PL; // Filtered File Data Displayed
        // public PacketParser PP;
        public UInt16 CurrentSync;
        public string LoadedFileTitle ;

        public ListBox lbPackets;

        public PacketTabPage()
        {
            PLLoaded = new PacketList();
            PL = new PacketList();
            lbPackets = new ListBox();
            lbPackets.Parent = this;
            lbPackets.Location = new System.Drawing.Point(0, 0);
            lbPackets.Size = new System.Drawing.Size(this.Width, this.Height);
            lbPackets.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lbPackets.DrawMode = DrawMode.OwnerDrawFixed;
            LoadedFileTitle = "Packets";
            CurrentSync = 0xFFFF;
        }


    }

}
