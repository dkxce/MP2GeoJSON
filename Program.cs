// code page 819 (ISO_8859-1:1987 Latin 1)
// code page 1252
//#define NoProps
//#define NoNames
//#define OnlyVector

using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace mp2geojson
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {            
            string inf = @"";
            if ((args != null) && (args.Length > 0) && (File.Exists(args[0]))) inf = args[0];
            else
            {
                System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                ofd.Title = "Select MP file";
                ofd.DefaultExt = ".mp";
                ofd.Filter = "MP files (*.mp)|*.mp|All types (*.*)|*.*";
                if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    inf = ofd.FileName;
                else
                    return;
            };
            string tmf = inf + ".tmp";
            string otf = inf + ".geojson";
            if ((args != null) && (args.Length > 1) && (File.Exists(args[1]))) otf = args[1];

            FileStream fs = new FileStream(inf, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.GetEncoding(1251));
            FileStream fw = new FileStream(tmf, FileMode.Create, FileAccess.ReadWrite);
            StreamWriter sw = new StreamWriter(fw, System.Text.Encoding.UTF8);
            sw.WriteLine("{ \"type\": \"FeatureCollection\", \"features\": [");

            ulong polylinesTTL = 0;
            ulong polylinesROADS = 0;
            ulong polylinesROADSwithNOD = 0;
            ulong polylinesROADSwithNODs = 0;
            ulong polylinesROADSwithRP = 0;
            ulong polylinesROADSwithRPs = 0;
            ulong roadsCount = 0;
            ulong roadSegments = 0;
            ulong withRoadID = 0;
            ulong withRoadIDs = 0;
            Hashtable CountryRegionsCities = new Hashtable();
            Hashtable Nodes = new Hashtable();
            List<int[]> restrictions = new List<int[]>();
            List<int[]> RoadWithNodes_FirstLast = new List<int[]>();

            Console.WriteLine("MP2GEOJSON [.mp] -> [.geojson] Convertor");
            Console.WriteLine("Файл MP должен быть пересохранен в GPSMapEdit");
            Console.WriteLine("");
            Console.WriteLine("Файл MP:          {0}", Path.GetFileName(inf));
            Console.WriteLine("Файл GeoJSON:     {0}", Path.GetFileName(otf));
            Console.WriteLine("");
            Console.WriteLine("Анализ дорог...");
            TBlock nb = null;
            while ((nb = getNextBlock(sr)) != null)
            {
                if ((nb.name == "Countries") || (nb.name == "Regions") || (nb.name == "Cities") || (nb.name == "ZipCodes"))
                    foreach (KeyValuePair<string, string> itm in nb.attributes)
                        CountryRegionsCities.Add(itm.Key, itm.Value);
                if (nb.name == "Restrict")
                {
                    string[] tp = nb.attributes["TraffPoints"].Split(new char[] { ',' });
                    string[] tr = nb.attributes["TraffRoads"].Split(new char[] { ',' });
                    restrictions.Add(new int[] { int.Parse(tp[0]), int.Parse(tp[1]), int.Parse(tp[2]), int.Parse(tr[0]), int.Parse(tr[1]) });
                };
                if ((nb.name == "POLYLINE"))
                {
                    // TOTAL POLYLINES
                    polylinesTTL++;                    

                    // TOTAL ROADS
                    if (nb.type > 0x1B) continue; // skip no roads
                    polylinesROADS++;                    
                    
                    // ROADS WITH NODES
                    int nc = 1;
                    while (nb.attributes.ContainsKey("Nod" + nc.ToString())) nc++; // multinode
                    nc--;
                    if (nc > 0)
                    {
                        polylinesROADSwithNOD++;
                        polylinesROADSwithNODs += (ulong)(nc - 1);
                    };

                    // ROADS WITH ROAD_ID
                    int roadID = 0;                    
                    if (nb.attributes.ContainsKey("RoadID")) roadID = int.Parse(nb.attributes["RoadID"]) * 1000;
                    if (roadID > 0)
                    {
                        withRoadID++;
                        if (nc > 0) withRoadIDs += (ulong)(nc - 1);
                    };

                    // ROADS with RouteParam
                    if (nb.attributes.ContainsKey("RouteParam"))
                    {
                        polylinesROADSwithRP++;
                        if (nc > 0) polylinesROADSwithRPs += (ulong)(nc - 1);
                    };

                    string sVector = ("{ \"type\": \"Feature\", \"geometry\": {\"type\": \"LineString\", \"coordinates\": [");
                    string sProperties = ("]},\"properties\": {");
                    sProperties += "\"ROAD_ID\":\"{ROAD_ID}\",\"POINTF\":\"{POINTF}\",\"POINTL\":\"{POINTL}\",\"TURN_RSTR\":\"{TURN_RSTR}\"";                   

                    // VECTOR
                    int _d = 0;
                    while (!nb.attributes.ContainsKey("Data"+_d.ToString())) if (_d == 9) break; else _d++;
                    string[] Data0 = nb.attributes["Data"+_d.ToString()].Split(new char[]{')'}, StringSplitOptions.RemoveEmptyEntries);                    
                    List<string> points = new List<string>();
                    for (int i = 0; i < Data0.Length; i++)
                    {
                        if (Data0[i][0] == ',') Data0[i] = Data0[i].Remove(0, 1);
                        if (Data0[i][0] == '(') Data0[i] = Data0[i].Remove(0, 1);
                        string[] cc = Data0[i].Split(new char[] { ',' });
                        points.Add(cc[1] + "," + cc[0]);
                    };

                    // Comments
                    foreach (KeyValuePair<string, string> itm in nb.comments)
                    {
                        int vlu = 0;
                        // ++ // added 23.06.2015 skip abnormal fields //
                        if((itm.Key.Length > 0) && (itm.Key[0] == '@') && (int.TryParse(itm.Key.Substring(1), out vlu))) continue;
                        // --
#if NoProps
#else
                            sProperties += String.Format(",\"{0}\": \"{1}\"", itm.Key, itm.Value.Replace("\"", "''").Replace("\r", "").Replace("\n", ""));
#endif
                    };
                    
                    // Attributes
                    foreach (KeyValuePair<string, string> itm in nb.attributes)
                    {
                        if (itm.Key.IndexOf("Nod") == 0) continue;
                        if (itm.Key.IndexOf("RoadID") == 0) continue;
                        if (itm.Key == "Data0") continue;
                        if (itm.Key == "Data1") continue;
                        if (itm.Key == "Data2") continue;
                        if (itm.Key == "Data3") continue;
                        if (itm.Key == "Data4") continue;
                        if (itm.Key == "Data5") continue;
                        if (itm.Key == "Data6") continue;
                        if (itm.Key == "Data7") continue;
                        if (itm.Key == "Data8") continue;
                        if (itm.Key == "Data9") continue;

                        if (itm.Key == "CityIdx")
                        {
                            string city = itm.Value;
                            string region = "";
                            string country = "";

                            if (CountryRegionsCities["City" + itm.Value] != null) city = CountryRegionsCities["City" + itm.Value].ToString();                                                        
                            if (CountryRegionsCities["RegionIdx" + itm.Value] != null) region = CountryRegionsCities["RegionIdx" + itm.Value].ToString();
                            if (CountryRegionsCities["CountryIdx" + region] != null) country = CountryRegionsCities["CountryIdx" + region].ToString();
                            if (CountryRegionsCities["Region" + region] != null) region = CountryRegionsCities["Region" + region].ToString();
                            if (CountryRegionsCities["Country" + country] != null) country = CountryRegionsCities["Country" + country].ToString();

#if NoNames
#else
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "CITY", city);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "REGION", region);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "COUNTRY", country);
#endif

                            continue;
                        };
                                                
                        if (itm.Key == "RouteParam")
                        {
                            string[] rp = itm.Value.Split(new char[] { ',' });
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "SPEED_LIMIT", (new string[] { "5","20","40","60","80","90","110","0" })[int.Parse(rp[0])]);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "ROAD_CLASS", rp[1]);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "ONE_WAY", rp[2]);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "TOLL_ROAD", rp[3]);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "NO_EMERCOM", rp[4]);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "NO_DELIVERY", rp[5]);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "NO_MOTOCAR", rp[6]);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "NO_BUS", rp[7]);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "NO_TAXI", rp[8]);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "NO_WALK", rp[9]);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "NO_CYCLE", rp[10]);
                            sProperties += String.Format(",\"{0}\": \"{1}\"", "NO_TRUCK", rp[11]);
                            continue;
                        };

                        // ALL OTHER
#if NoProps
#else
                        sProperties += String.Format(",\"{0}\": \"{1}\"", itm.Key, itm.Value.Replace("\"", "''").Replace("\r", "").Replace("\n", ""));
#endif
                    };
                    sProperties += ("}}");

                    if (nc < 2)
                    {
                        roadSegments++; // line to 1 line                      
                        string sApoints = "";
                        for (int i = 0; i < points.Count; i++)
                        {
                            if (i > 0) sApoints += ",";
                            sApoints += ("[" + points[i] + "]");
                        };
                        if (roadsCount++ > 0) sw.WriteLine(",");


#if OnlyVector
                        sProperties = ("]}}");
#endif

                        sw.Write(sVector + sApoints +
                            sProperties.Replace("{ROAD_ID}", roadID.ToString()).Replace("{POINTF}", "0").Replace("{POINTL}", "0")
                            );                        
                    }
                    else
                    {
                        roadSegments += (ulong)(nc - 1); // line to X split lines                        
                        for (int n = 1; n < nc; n++)
                        {
                            string[] nf = nb.attributes["Nod" + n.ToString()].Split(new char[] { ',' });
                            int cnf = int.Parse(nf[0]);
                            int inF = int.Parse(nf[1]);
                            Nodes[inF] = 1;

                            string[] nt = nb.attributes["Nod" + (n + 1).ToString()].Split(new char[] { ',' });
                            int cnt = int.Parse(nt[0]);
                            int inT = int.Parse(nt[1]);
                            Nodes[inT] = 1;

                            RoadWithNodes_FirstLast.Add(new int[] { roadID + n, inF, inT });

                            string sApoints = "";
                            for (int i = cnf; i <= cnt; i++)
                            {
                                if (i > cnf) sApoints += ",";
                                sApoints += ("[" + points[i] + "]");
                            };
                            if (roadsCount++ > 0) sw.WriteLine(",");

#if OnlyVector
                            sProperties = ("]}}");
#endif

                            sw.Write(sVector + sApoints +
                                sProperties.Replace("{ROAD_ID}", (roadID + n).ToString()).Replace("{POINTF}", nf[1]).Replace("{POINTL}", nt[1])
                                );
                        };
                    };                    
                };
                Console.SetCursorPosition(0, 7);
                Console.WriteLine("Запретов:         {0}", restrictions.Count);
                Console.WriteLine("Полилиний:        {0}", polylinesTTL);
                Console.WriteLine("Дорог:            {0}", polylinesROADS);
                Console.WriteLine("Дорог Nod>=2:     {0} ({1})", polylinesROADSwithNOD, polylinesROADSwithNODs);
                Console.WriteLine("Дорог ROAD_ID>0:  {0} ({1})", withRoadID, withRoadIDs);
                Console.WriteLine("Дорог RouteParam: {0} ({1})", polylinesROADSwithRP, polylinesROADSwithRPs);
                Console.WriteLine("Узлов:            {0}", Nodes.Count);
                Console.WriteLine("Отрезков дорог:   {0} ({1})", roadsCount, roadSegments);                
                Console.WriteLine("Выполнено:        {0:0.00}%", (((float)sr.BaseStream.Position) / ((float)sr.BaseStream.Length)) * 100);
            };
            Console.SetCursorPosition(0, 7);
            Console.WriteLine("Запретов:         {0}", restrictions.Count);
            Console.WriteLine("Полилиний:        {0}", polylinesTTL);
            Console.WriteLine("Дорог:            {0}", polylinesROADS);
            Console.WriteLine("Дорог Nod>=2:     {0}", polylinesROADSwithNOD);
            Console.WriteLine("Дорог ROAD_ID>0:  {0}", withRoadID);
            Console.WriteLine("Дорог RouteParam: {0}", polylinesROADSwithRP);
            Console.WriteLine("Узлов:            {0}", Nodes.Count);
            Console.WriteLine("Отрезков дорог:   {0} ({1})", roadsCount, roadSegments);
            Console.WriteLine("Выполнено:        {0:0.00}%", (((float)sr.BaseStream.Position) / ((float)sr.BaseStream.Length)) * 100);
            sw.WriteLine("\r\n]}");
            sw.Close();
            fw.Close();
            fs.Close();
            sr.Close();

            Console.WriteLine("");
            Console.WriteLine("Анализ запретов...");

            fs = new FileStream(tmf, FileMode.Open, FileAccess.Read);
            sr = new StreamReader(fs, System.Text.Encoding.UTF8);
            fw = new FileStream(otf, FileMode.Create, FileAccess.ReadWrite);
            sw = new StreamWriter(fw, System.Text.Encoding.UTF8);
            int lno = 0;
            int zco = 0;
            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();
                string NOT = "";
                int rid = line.IndexOf("ROAD_ID");
                if (rid > 0)
                {
                    lno++;
                    rid += 10;
                    int ptf = line.IndexOf("POINTF") + 9;
                    int ptl = line.IndexOf("POINTL") + 9;
                    int rtr = line.IndexOf("TURN_RSTR");
                    string rr = line.Substring(rid, ptf - rid - 12);
                    if (rr != "0")
                    {
                        string ff = line.Substring(ptf, ptl - ptf - 12);
                        string ll = line.Substring(ptl, rtr - ptl - 3);
                        int r = int.Parse(rr);
                        int f = int.Parse(ff);
                        int l = int.Parse(ll);
                        foreach (int[] noturn in restrictions)
                        {
                            // from FIRST
                            if ((noturn[0] == l) && (noturn[1] == f))
                            {
                                // no u-turn
                                if (noturn[2] == l) 
                                    NOT += (NOT.Length > 0 ? ";" : "") + "F" + rr;
                                else
                                {
                                    foreach (int[] rfs in RoadWithNodes_FirstLast)
                                    {
                                        if((rfs[1] == noturn[1]) && (rfs[2] == noturn[2]))
                                            NOT += (NOT.Length > 0 ? ";" : "") + "F" + rfs[0];
                                        if ((rfs[2] == noturn[1]) && (rfs[1] == noturn[2]))
                                            NOT += (NOT.Length > 0 ? ";" : "") + "F" + rfs[0];
                                    };
                                };
                            };
                            // from LAST
                            if ((noturn[0] == f) && (noturn[1] == l))
                            {
                                // no u-turn
                                if (noturn[2] == f)
                                    NOT += (NOT.Length > 0 ? ";" : "") + "L" + rr;
                                else
                                {
                                    foreach (int[] rfs in RoadWithNodes_FirstLast)
                                    {
                                        if ((rfs[1] == noturn[1]) && (rfs[2] == noturn[2]))
                                            NOT += (NOT.Length > 0 ? ";" : "") + "L" + rfs[0];
                                        if ((rfs[2] == noturn[1]) && (rfs[1] == noturn[2]))
                                            NOT += (NOT.Length > 0 ? ";" : "") + "L" + rfs[0];
                                    };
                                };
                            };
                        };
                    };                    
                };
                if (NOT != "") zco++;
                sw.WriteLine(line.Replace("{TURN_RSTR}",NOT));
                Console.SetCursorPosition(0, 18);
                Console.WriteLine("Пройдено дорог:   {0} из {1}", lno, roadsCount);
                Console.WriteLine("Учтено запретов:  {0} из {1}", zco, restrictions.Count);
                Console.WriteLine("Выполнено:        {0:0.00}%", (((float)sr.BaseStream.Position) / ((float)sr.BaseStream.Length)) * 100);                
            };
            Console.SetCursorPosition(0, 18);
            Console.WriteLine("Пройдено дорог:   {0} из {1}", lno, roadsCount);
            Console.WriteLine("Учтено запретов:  {0} из {1}", zco, restrictions.Count);
            Console.WriteLine("Выполнено:        {0:0.00}%", (((float)sr.BaseStream.Position) / ((float)sr.BaseStream.Length)) * 100);
            sw.Close();
            fw.Close();
            fs.Close();
            sr.Close();
            Console.WriteLine("");
            Console.WriteLine("Конвертация завершена");
            System.Threading.Thread.Sleep(15000);
        }

        static TBlock getNextBlock(StreamReader sr)
        {
            string rl = "";
            while (sr.BaseStream.Position < sr.BaseStream.Length)
            {
                string cl = sr.ReadLine().Trim();
                if(cl != "")
                {
                    rl += "\r\n" + cl;
                    if (cl.IndexOf("[END") == 0)
                        return new TBlock(rl);
                };
            };
            return null;
        }
    }

    public class TBlock
    {
        public string name;
        public Dictionary<string, string> comments = new Dictionary<string, string>();
        public Dictionary<string, string> attributes = new Dictionary<string, string>();
        public int type = 0;

        public TBlock(string text)
        {
            int bb = text.IndexOf("[");
            int eb = text.IndexOf("]", bb);
            this.name = text.Substring(bb + 1, eb - bb - 1);
            string[] lns = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string ln in lns)
            {
                string l = ln.Trim();
                if ((l.Length == 0) || (l[0] == '['))
                    continue;
                else
                {
                    if (l[0] == ';')
                    {
                        if (l.IndexOf("=") < 0)
                        {
                            comments.Add("@" + (comments.Count + 1).ToString(), l.Substring(1).Trim());
                        }
                        else
                        {
                            l = l.Substring(1).Trim();
                            string[] sep = l.Split(new char[] { '=' }, 2);
                            if ((sep != null) && (sep.Length == 2))
                            {
                                string cname = "@" + sep[0].Trim();
                                if(comments.ContainsKey(cname))
                                    comments[cname] += sep[1].Trim();
                                else
                                    comments.Add(cname, sep[1].Trim());
                            };
                        };
                    }
                    else
                    {
                        if (l.IndexOf("=") < 0)
                        {
                            attributes.Add("@" + (attributes.Count + 1).ToString(), l);
                        }
                        else
                        {
                            string[] sep = l.Split(new char[] { '=' }, 2);
                            if ((sep != null) && (sep.Length == 2))
                            {
                                if (sep[0].Trim() == "Type")
                                    type = Convert.ToInt32(sep[1],16);
                                if(!attributes.ContainsKey(sep[0].Trim()))
                                    attributes.Add(sep[0].Trim(), sep[1].Trim());
                            };
                        };
                    };
                };
            };
        }
    }
}
