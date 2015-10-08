using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Couchbase.Configuration;
using Couchbase.Core;
using Couchbase.N1QL;
using System.Web.Script.Serialization;
using Couchbase;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Formatting;
using System.Web;

namespace MarkAttendance
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select Service1.svc or Service1.svc.cs at the Solution Explorer and start debugging.
    public class Service1 : IService1
    {
        // CouchbaseClient client;
        private static readonly Cluster Cluster = new Cluster("couchbaseClients/couchbase");


        IBucket bucket;
       

        public Service1()
        {


            bucket = Cluster.OpenBucket("Testing");
          

        }

        public string temp()
        {
            return "working...";
        }

        public string postFunction(Stream read)
        {
 
            StreamReader reader = new StreamReader(read);
            string readToEnd = reader.ReadToEnd();
            string data = HttpUtility.UrlDecode(readToEnd);
            StreamWriter wtr = new StreamWriter(@"H:\check.txt",false);
            wtr.WriteLine(data);
            wtr.Close();
            string fullData = data.Remove(0, 8);
            JObject dataJobject = JObject.Parse(fullData);
            JToken token=dataJobject["pusher"];
            DateTime Datetime = DateTime.Now;
            postAttendance(token["email"].ToString(), null, Datetime.ToString("yyyy-MM-dd T HH:mm:ss"), 6);
            return token["email"].ToString();
        }
        
        public List<string> postAttendance(string email, string user_id, string timeIn, int location)
        {
            var queryRequest = new QueryRequest();
            DateTime TimeIn = DateTime.Now;
            List<string> values = new List<string>();
            TimeSpan work;
            try
            {
                if (timeIn.CompareTo("") != 0)
                {

                    TimeIn = DateTime.Parse(HttpUtility.UrlDecode(timeIn));
                }
                string userCond = "";
                userCond = " and user_id=$user_id ";
                if (user_id != null && !user_id.Equals(""))
                {

                    queryRequest.AddNamedParameter("user_id", user_id);

                }
                else if (email != null && !email.Equals(""))
                {
                    queryRequest.AddNamedParameter("email", email);

                    var queryGetUserId = new QueryRequest();
                    queryGetUserId.Statement("SELECT META(Testing).id AS id FROM `Testing`  WHERE email=$email")
                        .AddNamedParameter("email", email);
                    IQueryResult<dynamic> resultId = bucket.Query<dynamic>(queryGetUserId);
                    if (resultId.Rows.Count() > 0)
                    {
                        JObject userAttendanceData = JsonConvert.DeserializeObject<JObject>(resultId.Rows.ElementAt(0) + "");
                        values.Add(userAttendanceData["id"].ToString());
                        queryRequest.AddNamedParameter("user_id", userAttendanceData["id"].ToString());
                        user_id = userAttendanceData["id"].ToString();
                    }
                    else
                    {
                        values.Add("No User Found");
                        return values;
                    }
                }
                else
                {
                    values.Add("No User Found");
                    return values;
                }
                string dat=TimeIn.ToString("yyyy-MM-dd");
                UserAttendance newAttendance = null;
                queryRequest.Statement("SELECT META(Testing).id AS id,* FROM `Testing` WHERE date=$date2 " + userCond)
                        .AddNamedParameter("date2",dat );

                IQueryResult<dynamic> result = bucket.Query<dynamic>(queryRequest);

                if (result.Rows.Count() == 0)
                {
                    DateTime defTime = DateTime.Parse(TimeIn.ToString("yyyy/MM/dd") + "T17:00:00");
                    work = defTime.Subtract(TimeIn);
                    newAttendance = new UserAttendance
                   {
                       user_id = user_id,
                       date = dat,
                       marked_at = new List<DateTime>(),
                       time_in = TimeIn.ToString("yyyy-MM-ddTHH:mm:ss"),
                       time_out = "",
                       default_out = defTime.ToString(),
                       shifttotalhours = "8",
                       worktotalhours = work.Hours + ":" + work.Minutes,
                       doctype = "user_attendance",
                       channels = new[] { "attendance" },

                       client = "Esajee",
                       shiftId = "1",
                       shiftName = "Default",
                       location = location
                   };
                    newAttendance.marked_at.Add(TimeIn);
                    values.Add(user_id);
         //           toServerPost(newAttendance, "","POST");
                }
                else
                {
                    JObject userAttendanceData = JsonConvert.DeserializeObject<JObject>(result.Rows.ElementAt(0)+ "");
                    string docId = "";
                    string revId = "";
                    docId = userAttendanceData["id"].ToString();
                    revId = userAttendanceData["Testing"]["_sync"]["rev"].ToString();
                    newAttendance = JsonConvert.DeserializeObject<UserAttendance>(JsonConvert.SerializeObject(userAttendanceData["Testing"]));
                    DateTime markTimeIn = DateTime.Parse(newAttendance.time_in);
                   
                    if (newAttendance != null)
                    {

                        newAttendance.time_out = TimeIn.ToString("yyyy-MM-ddTHH:mm:ss");
                        TimeSpan work12 = TimeIn.Subtract(markTimeIn);
                        newAttendance.worktotalhours = work12.Hours + ":" + work12.Minutes;
                        if (newAttendance.marked_at != null)
                        {
                            newAttendance.marked_at.Add(TimeIn);
                        }
                        values.Add(toServerPost(newAttendance,docId + "?rev="+revId,"PUT"));
                    }
                    else
                    {
                        values.Add("View Not Working");
                    }

                }
            }
            catch (Exception re)
            {
                values.Add("Times convertion failed");
                return values;
            }
            return values;
        }


        public List<string> fromFile(int location)
        {
            List<string> statsInsert = new List<string>();
            StreamReader stReader = new StreamReader(@"C:\ATTENDNCE.csv");
            string headLine = stReader.ReadLine();
            string[] dateIds = headLine.Split(',');
            string date = dateIds[0];
            DateTime asdasd = DateTime.Parse(date);
            string month = asdasd.ToString("yyyy-MM");
            string line = "";
            while ((line = stReader.ReadLine()) != null)
            {
                string[] time = line.Split(',');
                for (int i = 1; i < time.Length; i += 3)
                {
                    try
                    {
                        if (time[i].CompareTo("") != 0 && time[i + 1].CompareTo("") != 0)
                        {
                            string inTime = "";
                            string outTime = "";
                            DateTime timeIn;
                            DateTime timeOut;
                            if (int.Parse(time[0]) > 9)
                            {
                                Console.WriteLine("");
                            }
                            if (int.Parse(time[0]) < 10)
                            {
                                date = month + "-" + "0" + time[0];
                                inTime = month + "-" + "0" + time[0] + " " + time[i];
                                outTime = month + "-" + "0" + time[0] + " " + time[i + 1];
                            }
                            else
                            {
                                date = month + "-" + time[0];
                                inTime = month + "-" + time[0] + " " + time[i];
                                outTime = month + "-" + time[0] + " " + time[i + 1];
                            }
                            timeIn = DateTime.Parse(inTime);

                            timeOut = DateTime.Parse(outTime);
                            TimeSpan timeSp = timeOut.Subtract(timeIn);
                            UserAttendance newAttendance = new UserAttendance
                             {
                                 user_id = dateIds[i],
                                 date = date,
                                 marked_at = new List<DateTime>(),
                                 time_in = timeIn.ToString("yyyy-MM-ddTHH:mm:ss"),
                                 time_out = timeOut.ToString("yyyy-MM-ddTHH:mm:ss"),
                                 default_out = month + "-" + time[0] + "T17:00:00",
                                 shifttotalhours = "8",
                                 worktotalhours = timeSp.Hours + ":" + timeSp.Minutes,
                                 doctype = "user_attendance",
                                 channels = new[] { "attendance" },

                                 client = "Esajee",
                                 shiftId = "1",
                                 shiftName = "Default",
                                 location =location

                             };
                            newAttendance.marked_at.Add(DateTime.Now);
                          //  toServerPost(newAttendance, "","POST");
                        }
                        else
                        {
                            statsInsert.Add("Date not Found");
                        }

                    }
                    catch (Exception re)
                    {
                        statsInsert.Add(time[0] + ":" + time[i]);
                    }
                }
            }

            return statsInsert;
        }
        /*
                public void PutObject(string postUrl, object payload)
                {
                    var request = (HttpWebRequest)WebRequest.Create(postUrl);
                    request.Method = "PUT";
                    request.ContentType = "application/json";
                    if (payload != null)
                    {
                      //  request.ContentLength = JsonConvert.SerializeObject(payload).Length;
                        Stream dataStream = request.GetRequestStream();
                        Serialize(dataStream, payload);
                        dataStream.Close();
                    }

                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    string returnString = response.StatusCode.ToString();
                }
       
                public void Serialize(Stream output, object input)
                {
                    var ser = new DataContractSerializer(input.GetType());
                    ser.WriteObject(output, JsonConvert.SerializeObject(input));
                }
         */

        public string toServerPost(UserAttendance userAttendance, string id,string method)
        {

            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://192.168.1.202:4984/db/" + id);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.AllowWriteStreamBuffering = true;
            httpWebRequest.PreAuthenticate = true;
            httpWebRequest.Headers.Add("Authorization", "Basic YXR0ZW5kYW5jZTphdHRlbmRhbmNl");
            httpWebRequest.Method = method;
            httpWebRequest.MediaType = "json";

            httpWebRequest.Credentials = new NetworkCredential("attendance", "attendance");

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                var serializer = new JavaScriptSerializer();
                streamWriter.Write(serializer.Serialize(userAttendance));
                streamWriter.Flush();
                streamWriter.Close();
            }
            try
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback = (senderX, certificate, chain, sslPolicyErrors) => { return true; };
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse() as HttpWebResponse;
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    return JsonConvert.SerializeObject(result);
                }
            }
            catch (Exception er)
            {
                Console.WriteLine(er.ToString());
                return "false";
            }
            return "false";

        }


        public List<Location> GetLocations()
        {

            IDocumentResult<Locations> result = bucket.GetDocument<Locations>("attendance::locations");
            return result.Content.locations;
        }

        public string deleteExtraDoc(string id)
        {
            string docId = "";

            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://192.168.1.202:4984/db/" + id);
            httpWebRequest.ContentType = "application/x-www-form-urlencoded";
            httpWebRequest.Headers.Add("Authorization", "Basic YXR0ZW5kYW5jZTphdHRlbmRhbmNl");
            httpWebRequest.Method = "DELETE";

            httpWebRequest.Credentials = new NetworkCredential("attendance", "attendance");
            /*
                        using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                        {
                            var serializer = new JavaScriptSerializer();
                            streamWriter.Write(serializer.Serialize(userAttendance));
                            streamWriter.Flush();
                            streamWriter.Close();
                        }*/
            try
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback = (senderX, certificate, chain, sslPolicyErrors) => { return true; };
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse() as HttpWebResponse;
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    return JsonConvert.SerializeObject(result);
                }
            }
            catch (Exception er)
            {
                Console.WriteLine(er.ToString());
                docId = "conflict";

            }

            return docId;
        }

        public List<string> getDeleteDocIdsFromView()
        {
            List<string> docIds = new List<string>();
            for (int j = 1; j <= 30; j++)
            {
                string dateNumber = "" + j;
                if (j < 10)
                {
                    dateNumber = "0" + j;
                }
                object[] sldkf = new object[2];
                sldkf[0] = 6;
                sldkf[1] = "2015-09-" + dateNumber;
                var query = bucket.CreateQuery("testing", "test", true);
                query.StartKey(sldkf);
                query.EndKey(sldkf);
                var result = bucket.Query<dynamic>(query);
                if (result.Rows.Count() > 0)
                {
                    for (int i = 0; i < result.Rows.Count(); i++)
                    {
                        string rw = result.Rows.ElementAt(i).Value;
                        string sendData = result.Rows.ElementAt(i).Id;
                        docIds.Add(deleteExtraDoc(sendData + "?rev=" + rw));
                    }
                }
            }
            return docIds;

        }
/*
        public string GetDateAttendance(string userId, string date, UserAttendance updateAttendance)
        {
            object[] keysData = new object[2];
            keysData[0] = userId;
            keysData[1] = date;
            string sendData = null;
            var query = bucket.CreateQuery("testing", "get_attendance", true);
            query.StartKey(keysData);
            query.EndKey(keysData);
            var result = bucket.Query<dynamic>(query);
            if (result.Rows.Count() > 0)
            {
                string rw = result.Rows.ElementAt(0).Value;
                sendData = result.Rows.ElementAt(0).Id;
            }

            return sendData;
        }
*/
        public CompositeType GetDataUsingDataContract(CompositeType composite)
        {
            if (composite == null)
            {
                throw new ArgumentNullException("composite");
            }
            if (composite.BoolValue)
            {
                composite.StringValue += "Suffix";
            }
            return composite;
        }
    }

    public class shifts
    {
        public string name { get; set; }
        public string start { get; set; }
        public string end { get; set; }
        public string def_time { get; set; }
        public int margin { get; set; }
        public string TotalShiftHours { get; set; }
        public int id { get; set; }
    }

    public class sendShift
    {
        public string name { get; set; }
        public string start { get; set; }
        public string end { get; set; }
        public int id { get; set; }
    }
    public class Locations
    {
        [JsonProperty("locations")]
        public List<Location> locations { get; set; }
    }

    public class Location
    {
        [JsonProperty("id")]
        public int id { get; set; }

        [JsonProperty("name")]
        public string name { get; set; }

    }

    public class User
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("location")]
        public int Location { get; set; }

        [JsonProperty("date")]
        public string Date { get; set; }

        [JsonProperty("marked_at")]
        public IList<DateTime> MarkedAt { get; set; }

        [JsonProperty("time_in")]
        public DateTime TimeIn { get; set; }

        [JsonProperty("time_out")]
        public DateTime? TimeOut { get; set; }

        [JsonProperty("default_out")]
        public DateTime default_out { get; set; }

        [JsonProperty("doctype")]
        public string Doctype { get; set; }


        [JsonProperty("channels")]
        public IList<string> Channels { get; set; }

        [JsonProperty("client")]
        public string Client { get; set; }

        [JsonProperty("shifttotalhours")]
        public string ShiftTotalHours { get; set; }

        [JsonProperty("worktotalhours")]
        public string WorkTotalHours { get; set; }
        [JsonProperty("shiftId")]
        public string shiftId { get; set; }

        [JsonProperty("shiftName")]
        public string shiftName { get; set; }

        [JsonProperty("userLocation")]
        public int userLocation { get; set; }

    }


    public class UserAttendance
    {

        [JsonProperty("user_id")]
        public string user_id { get; set; }

        [JsonProperty("location")]
        public int location { get; set; }

        [JsonProperty("date")]
        public string date { get; set; }

        [JsonProperty("marked_at")]
        public IList<DateTime> marked_at { get; set; }

        [JsonProperty("time_in")]
        public string time_in { get; set; }

        [JsonProperty("time_out")]
        public string time_out { get; set; }

        [JsonProperty("doctype")]
        public string doctype { get; set; }


        [JsonProperty("channels")]
        public IList<string> channels { get; set; }

        [JsonProperty("client")]
        public string client { get; set; }

        [JsonProperty("shifttotalhours")]
        public string shifttotalhours { get; set; }

        [JsonProperty("worktotalhours")]
        public string worktotalhours { get; set; }

        [JsonProperty("shiftId")]
        public string shiftId { get; set; }

        [JsonProperty("shiftName")]
        public string shiftName { get; set; }


        [JsonProperty("default_out")]
        public string default_out { get; set; }
    }
}
