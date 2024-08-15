// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Quartz;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Newtonsoft.Json;
using System.Net;
using OfficeOpenXml;
using System.Drawing;
using OfficeOpenXml.Style;
using RestSharp;
using Rock.Security;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;
using OfficeOpenXml.FormulaParsing.LexicalAnalysis;
using System.Text;

namespace org.crossingchurch.HubspotIntegration.Jobs
{
    /// <summary>
    /// Job to supply hubspot contacts with a rock_id to the pull related information.
    /// </summary>
    [DisplayName("Hubspot Integration: Match Records")]
    [Description("This job only supplies Hubspot contacts with a Rock ID and adds potential matches to an excel spreadsheet for further investigation.")]
    [DisallowConcurrentExecution]

    [TextField("AttributeKey", "The attribute key for the global attribute that contains the HubSpot API Key. The attribute must be encrypted.", true, "HubspotAPIKeyGlobal")]
    [TextField("Business Unit", "Hubspot Business Unit value", true, "0")]
    [TextField("Potential Matches File Name", "Name of the file for this job to list potential matches for cleaning", true, "Potential_Matches")]
    public class HubspotIntegrationMatching : IJob
    {
        private string key { get; set; }
        private List<HSContactResult> contacts { get; set; }
        private int request_count { get; set; }
        private string businessUnit { get; set; }

        /// <summary> 
        /// Empty constructor for job initialization
        /// <para>
        /// Jobs require a public empty constructor so that the
        /// scheduler can instantiate the class whenever it needs.
        /// </para>
        /// </summary>
        public HubspotIntegrationMatching()
        {
        }

        /// <summary>
        /// Job that will run quick SQL queries on a schedule.
        /// 
        /// Called by the <see cref="IScheduler" /> when a
        /// <see cref="ITrigger" /> fires that is associated with
        /// the <see cref="IJob" />.
        /// </summary>
        public virtual void Execute(IJobExecutionContext context)
        {
            JobDataMap dataMap = context.JobDetail.JobDataMap;

            // Get HS API Token
            string attrKey = dataMap.GetString("AttributeKey");
            string attrValue = GlobalAttributesCache.Get().GetValue(attrKey);
            key = Encryption.DecryptString(attrValue);

            // Don't see this being used for anything...
            var current_id = 0;

            // Setup Excel
            ExcelPackage excel = new ExcelPackage();
            ExcelWorksheet worksheet = SetupWorksheet(excel);
            var row = 2;

            // Setup Context & Services
            RockContext _context = new RockContext();
            PersonService personService = new PersonService(_context);
            PersonSearchKeyService personSearchKeyService = new PersonSearchKeyService(_context);

            // Get list of all contacts from HubSpot
            contacts = new List<HSContactResult>();
            request_count = 0;
            string apiUrl = "https://api.hubapi.com/crm/v3/objects/contacts?limit=100&properties=email,firstname,lastname,phone,rock_person_id,rock_record_status,has_potential_rock_match,createdate,lastmodifieddate";
            GetContacts(apiUrl);
            Debug.WriteLine("Contacts returned: " + contacts.Count());
            WriteToLog(string.Format("Total Contacts to Match: {0}", contacts.Count()));

            // 5 cases, record statistics for each
            // HS person without person id we find a match for
            int idSet = 0;
            // HS person with a person id that no longer exists in Rock
            int idRemoved = 0;
            // HS person with a person id that exists in Rock today
            int idCorrect = 0;
            // HS person has no match in rock
            int idEmpty = 0;
            // Inserted new HS person that was only in rock
            int idAdded = 0;


            // Process each contact in HubSpot
            for ( var i = 0; i < contacts.Count(); i++ )
            {
                var contact = contacts[i];

                Stopwatch watch = new Stopwatch();
                watch.Start();

                Debug.WriteLine("Operating on contact: " + contact);

                if ( contact.properties.rock_person_id != "" )
                {
                    Person existingPerson = personService.Get(contacts[i].properties.rock_person_id);
                    if (existingPerson == null)
                    {
                        // Person not found
                        Debug.WriteLine("Contact with id not found in Rock. Clearing Id.");
                        // Clear the person id in HubSpot
                        var properties = new List<HubspotPropertyUpdate>() { new HubspotPropertyUpdate() { property = "rock_person_id", value = "" } };
                        var url = $"https://api.hubapi.com/crm/v3/objects/contacts/{contact.id}";
                        UpdateHsContact(current_id, url, properties, 0);
                        idRemoved++;
                    }
                    else
                    {
                        // Person still exists in DB
                        // No need to continue processing this person
                        Debug.WriteLine("Contact with id found in Rock. Skipping contact");
                        idCorrect++;
                        break;
                    }
                } 
                
                Person person = null;
                bool hasMultiEmail = false;
                
                List<int> matchingIds = FindPersonIds(contact, _context);
                if ( matchingIds.Count > 1 )
                {
                    hasMultiEmail = true;
                }

                if ( matchingIds.Count == 1 )
                {
                    person = personService.Get(matchingIds.First());
                    // New single match found, increment the counter!
                    idSet++;
                }

                Debug.WriteLine("Matching People Found: " + matchingIds.ToString());
                WriteToLog(string.Format("    After SQL: {0}", watch.ElapsedMilliseconds));

                // Atempt to match 1:1 based on email history making sure we exclude emails with multiple matches in the person table
                if ( person == null && !hasMultiEmail )
                {
                    string email = contact.properties.email.ToLower();
                    var matches = personSearchKeyService.Queryable().Where(k => k.SearchTypeValueId == 3497 && k.SearchValue == email).Select(k => k.PersonAlias.PersonId).Distinct().ToList();

                    if ( matches != null )
                    {
                        Debug.WriteLine("PersonSearchKeyService match count: " + matches.Count());
                        if ( matches.Count() == 1 )
                        {
                            // If 1:1 Email match and Hubspot has no other info, make it a match
                            if ( String.IsNullOrEmpty(contact.properties.firstname) && String.IsNullOrEmpty(contact.properties.lastname) )
                            {
                                person = personService.Get(matches.First());
                                // New single match found, increment the counter!
                                idSet++;
                            }
                        }
                    }

                }
                WriteToLog(string.Format("    After Email History: {0}", watch.ElapsedMilliseconds));


                // Try to mark people that are potential matches, only people who can at least match email or phone and one other thing, only once
                bool inBucket = false;
                if (person == null && contact.properties.has_potential_rock_match != "True")
                {
                    // Matches phone number and one other piece of info
                    if (!String.IsNullOrEmpty(contact.properties.phone))
                    {
                        var phone_matches = personService.Queryable().Where(p => p.PhoneNumbers.Select(pn => pn.Number).Contains(contact.properties.phone)).ToList();
                        if (phone_matches.Count() > 0)
                        {
                            phone_matches = phone_matches.Where(p => CustomEquals(p.FirstName, contact.properties.firstname) || CustomEquals(p.NickName, contact.properties.firstname) || CustomEquals(p.Email, contact.properties.email) || CustomEquals(p.LastName, contact.properties.lastname)).ToList();
                            for (var j = 0; j < phone_matches.Count(); j++)
                            {
                                //Save this information in the excel sheet....
                                SaveData(worksheet, row, phone_matches[j], contact);
                                inBucket = true;
                                row++;
                            }
                        }
                    }

                    // Matches email and one other piece of info
                    var email_matches = personService.Queryable().ToList().Where(p =>
                    {
                        return CustomEquals(p.Email, contact.properties.email);
                    }).ToList();
                    if (email_matches.Count() > 0)
                    {
                        email_matches = email_matches.Where(p => CustomEquals(p.FirstName, contact.properties.firstname) || CustomEquals(p.NickName, contact.properties.firstname) || (!String.IsNullOrEmpty(contact.properties.phone) && p.PhoneNumbers.Select(pn => pn.Number).Contains(contact.properties.phone)) || CustomEquals(p.LastName, contact.properties.lastname)).ToList();
                        for (var j = 0; j < email_matches.Count(); j++)
                        {
                            //Save this information in the excel sheet....
                            SaveData(worksheet, row, email_matches[j], contact);
                            inBucket = true;
                            row++;
                        }
                    }
                    if (inBucket)
                    {
                        WriteToLog(string.Format("    Added data to bucket!"));
                    }
                    WriteToLog(string.Format("    After Excel: {0}", watch.ElapsedMilliseconds));
                }

                // Schedule HubSpot update if 1:1 match
                if ( person != null )
                {
                    var properties = new List<HubspotPropertyUpdate>() { new HubspotPropertyUpdate() { property = "rock_person_id", value = person.Id.ToString() } };

                    // If the Hubspot Contact does not have FirstName, LastName, or Phone Number we want to update those...
                    if ( String.IsNullOrEmpty(contact.properties.firstname) )
                    {
                        properties.Add(new HubspotPropertyUpdate() { property = "firstname", value = person.NickName });
                    }
                    if ( String.IsNullOrEmpty(contact.properties.lastname) )
                    {
                        properties.Add(new HubspotPropertyUpdate() { property = "lastname", value = person.LastName });
                    }
                    if ( String.IsNullOrEmpty(contact.properties.phone) )
                    {
                        PhoneNumber mobile = person.PhoneNumbers.FirstOrDefault(n => n.NumberTypeValueId == 12);
                        if ( mobile != null && !mobile.IsUnlisted )
                        {
                            properties.Add(new HubspotPropertyUpdate() { property = "phone", value = mobile.NumberFormatted });
                        }
                    }
                    var url = $"https://api.hubapi.com/crm/v3/objects/contacts/{contact.id}";
                    UpdateHsContact(current_id, url, properties, 0);
                    WriteToLog(string.Format("    After Request (Id and Props): {0}", watch.ElapsedMilliseconds));
                }
                else
                {
                    if ( inBucket )
                    {
                        //We don't have an exact match but we have guesses, so update Hubspot to reflect that.
                        var properties = new List<HubspotPropertyUpdate>() { new HubspotPropertyUpdate() { property = "potential_rock_match", value = "True" } };
                        var url = $"https://api.hubapi.com/crm/v3/objects/contacts/{contact.id}";
                        UpdateHsContact(current_id, url, properties, 0);

                        WriteToLog(string.Format("    After Request (Label): {0}", watch.ElapsedMilliseconds));
                    }
                }
                WriteToLog(string.Format("    End of Iteration: {0}", watch.ElapsedMilliseconds));
                watch.Stop();
            }

            // Add contacts for people unknown to HubSpot
            HashSet<int> hsPersonIds = new HashSet<int>();
            foreach ( var contact in contacts)
            {
                hsPersonIds.Add(contact.properties.rock_person_id.AsInteger());
            }
            
            int[] personIds = GetAllPersonIds(_context);
            foreach ( int id in personIds )
            {
                if (!hsPersonIds.Contains(id))
                {
                    Person person = personService.Get(id);
                    if (person != null)
                    {
                        List<HubspotPropertyUpdate> properties = new List<HubspotPropertyUpdate>();
                        properties.Add(new HubspotPropertyUpdate() { property = "firstname", value = person.NickName });
                        properties.Add(new HubspotPropertyUpdate() { property = "lastname", value = person.LastName });
                        properties.Add(new HubspotPropertyUpdate() { property = "rock_person_id", value = person.Id.ToString() });
                        PhoneNumber mobile = person.PhoneNumbers.FirstOrDefault( n => n.NumberTypeValueId == 12 );
                        if ( mobile != null && !mobile.IsUnlisted && mobile.IsMessagingEnabled )
                        {
                            properties.Add( new HubspotPropertyUpdate() { property = "phone", value = mobile.NumberFormatted } );
                        }

                        string email = person.Email;
                        if ( person.CanReceiveEmail( true ) )
                        {
                            properties.Add( new HubspotPropertyUpdate() { property = "email", value = email } );
                        }

                        var url = $"https://api.hubapi.com/crm/v3/objects/contacts/";
                        AddHsContact(url, properties, 0);
                        idAdded++;
                    }
                }
            }

            // Output Job Stats
            var resultMsg = new StringBuilder();
            resultMsg.AppendFormat( "{0} contacts with Rock person id added or updated", idSet );
            resultMsg.AppendFormat( ", {0} contacts with Rock person id removed", idRemoved );
            resultMsg.AppendFormat( ", {0} contacts with Rock person id that is valid", idCorrect );
            resultMsg.AppendFormat( ", {0} contacts with no match in Rock", idEmpty );
            resultMsg.AppendFormat( ", {0} new contacts imported into HubSpot", idAdded );
            context.UpdateLastStatusMessage(resultMsg.ToString());

            // Write excel file
            byte[] sheetbytes = excel.GetAsByteArray();
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Plugins\\org_thecrossingchurch\\Assets\\Generated\\" + dataMap.GetString("PotentialMatchesFileName") + ".xlsx";
            System.IO.FileInfo file = new System.IO.FileInfo(path);
            file.Directory.Create();
            System.IO.File.WriteAllBytes(path, sheetbytes);
        }

        private void AddHsContact(string url, List<HubspotPropertyUpdate> properties, int attempt)
        {
            // Add new Hubspot Contact
            try
            {
                var client = new RestClient(url);
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AddHeader("accept", "application/json");
                request.AddHeader("content-type", "application/json");
                request.AddHeader("Authorization", $"Bearer {key}");
                request.AddParameter("application/json", $"{{\"properties\": {{ {String.Join(",", properties.Select(p => $"\"{p.property}\": \"{p.value}\""))} }} }}", ParameterType.RequestBody);
                IRestResponse response = client.Execute(request);
                if ( ( int )response.StatusCode == 429 )
                {
                    if ( attempt < 3 )
                    {
                        Thread.Sleep(9000);
                        AddHsContact(url, properties, attempt + 1);
                    }
                }
                if ( response.StatusCode != HttpStatusCode.OK )
                {
                    throw new Exception(response.Content);
                }
            }
            catch ( Exception e )
            {
                var json = $"{{\"properties\": {JsonConvert.SerializeObject(properties)} }}";
                ExceptionLogService.LogException(new Exception($"Hubspot Sync Error{Environment.NewLine}{e}{Environment.NewLine}Exception from Request:{Environment.NewLine}{e.Message}{Environment.NewLine}Request:{Environment.NewLine}{json}{Environment.NewLine}"));
            }
        }

        private void UpdateHsContact(int current_id, string url, List<HubspotPropertyUpdate> properties, int attempt)
        {
            // Update the Hubspot Contact
            try
            {
                var client = new RestClient(url);
                client.Timeout = -1;
                var request = new RestRequest(Method.PATCH);
                request.AddHeader("accept", "application/json");
                request.AddHeader("content-type", "application/json");
                request.AddHeader("Authorization", $"Bearer {key}");
                request.AddParameter("application/json", $"{{\"properties\": {{ {String.Join(",", properties.Select(p => $"\"{p.property}\": \"{p.value}\""))} }} }}", ParameterType.RequestBody);
                IRestResponse response = client.Execute(request);
                if ( ( int )response.StatusCode == 429 )
                {
                    if ( attempt < 3 )
                    {
                        Thread.Sleep(9000);
                        UpdateHsContact(current_id, url, properties, attempt + 1);
                    }
                }
                if ( response.StatusCode != HttpStatusCode.OK )
                {
                    throw new Exception(response.Content);
                }
            }
            catch ( Exception e )
            {
                var json = $"{{\"properties\": {JsonConvert.SerializeObject(properties)} }}";
                ExceptionLogService.LogException(new Exception($"Hubspot Sync Error{Environment.NewLine}{e}{Environment.NewLine}Current Id: {current_id}{Environment.NewLine}Exception from Request:{Environment.NewLine}{e.Message}{Environment.NewLine}Request:{Environment.NewLine}{json}{Environment.NewLine}"));
            }
        }

        private void WriteToLog(string message)
        {
            string logFile = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Logs/HubSpotMatchLog.txt");
            using ( System.IO.FileStream fs = new System.IO.FileStream(logFile, System.IO.FileMode.Append, System.IO.FileAccess.Write) )
            {
                using ( System.IO.StreamWriter sw = new System.IO.StreamWriter(fs) )
                {
                    sw.WriteLine(message);
                }
            }

        }

        private void GetContacts(string url)
        {
            request_count++;
            var contactClient = new RestClient(url);
            contactClient.Timeout = -1;
            var contactRequest = new RestRequest(Method.GET);
            contactRequest.AddHeader("Authorization", $"Bearer {key}");
            IRestResponse contactResponse = contactClient.Execute(contactRequest);
            var contactResults = JsonConvert.DeserializeObject<HSContactQueryResult>(contactResponse.Content);
            // Original - Contacts with emails that do not already have Rock IDs in the desired business unit
            // Now - LOOK AT EVERY CONTACT
            // contacts.AddRange(contactResults.results.Where(c => c.properties.email != null && c.properties.email != "" && ( c.properties.rock_person_id == null || c.properties.rock_person_id == "" || c.properties.rock_person_id == "0" )).ToList());
            if ( contactResults.paging != null && contactResults.paging.next != null && !String.IsNullOrEmpty(contactResults.paging.next.link) && request_count < 2000 )
            {
                GetContacts(contactResults.paging.next.link);
            }
        }

        private List<int> FindPersonIds(HSContactResult contact, RockContext _context)
        {
            using ( RockContext context = _context )
            {
                SqlParameter[] sqlParams = new SqlParameter[] {
                    new SqlParameter( "@first_name", contact.properties.firstname ?? "" ),
                    new SqlParameter( "@last_name", contact.properties.lastname ?? "" ),
                    new SqlParameter( "@email", contact.properties.email ?? "" ),
                    new SqlParameter( "@mobile_number", contact.properties.phone ?? "" ),
                };
                var query = context.Database.SqlQuery<int>($@"
                    DECLARE @matches int = (SELECT COUNT(*) FROM Person WHERE Email = @email);

                    SELECT DISTINCT Person.Id
                    FROM Person
                            LEFT OUTER JOIN PhoneNumber ON Person.Id = PhoneNumber.PersonId
                    WHERE ((@email IS NOT NULL AND @email != '') AND
                        (Email = @email AND
                            (((@first_name IS NULL OR @first_name = '') AND (@last_name IS NULL OR @last_name = '') AND @matches = 1) OR
                            ((@first_name IS NOT NULL AND @first_name != '' AND
                            (FirstName = @first_name OR NickName = @first_name)) OR
                            (@last_name IS NOT NULL AND @last_name != '' AND LastName = @last_name) OR
                            (@mobile_number IS NOT NULL AND @mobile_number != '' AND
                            (Number = @mobile_number OR PhoneNumber.NumberFormatted = @mobile_number))))))
                    ", sqlParams).ToList();
                return query;
            }
        }

        private int[] GetAllPersonIds(RockContext _context)
        {
            using ( RockContext context = _context )
            {
                var query = context.Database.SqlQuery<int>($@"
                    SELECT Id
                    FROM [dbo].[Person]
                    Where Age >= 18
                    AND RecordStatusValueId = 3
                    ").ToArray();
                return query;
            }
        }

        private ExcelWorksheet SetupWorksheet(ExcelPackage excel)
        {
            //Set up Static Report of Potential Matches
            excel.Workbook.Properties.Title = "Potential Matches";
            excel.Workbook.Properties.Author = "Rock";
            ExcelWorksheet worksheet = excel.Workbook.Worksheets.Add("Potential Matches");
            worksheet.PrinterSettings.LeftMargin = .5m;
            worksheet.PrinterSettings.RightMargin = .5m;
            worksheet.PrinterSettings.TopMargin = .5m;
            worksheet.PrinterSettings.BottomMargin = .5m;
            var headers = new List<string> { "HubSpot FirstName", "Rock FirstName", "HubSpot LastName", "Rock LastName", "HubSpot Email", "Rock Email", "HubSpot Phone", "Rock Phone", "HubSpot Connection Status", "Rock Connection Status", "HubSpot Link", "Rock Link", "HubSpot CreatedDate", "Rock Created Date", "HubSpot Modified Date", "Rock Modified Date", "Rock ID" };
            var h = 1;
            foreach ( var header in headers )
            {
                worksheet.Cells[1, h].Value = header;
                h++;
            }
            return worksheet;
        }

        private ExcelWorksheet ColorCell(ExcelWorksheet worksheet, int row, int col)
        {
            //Color the Matching Data Green 
            Color c = System.Drawing.ColorTranslator.FromHtml("#9CD8BC");
            worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(c);
            return worksheet;
        }

        private ExcelWorksheet SaveData(ExcelWorksheet worksheet, int row, Person person, HSContactResult contact)
        {
            //Add FirstNames
            worksheet.Cells[row, 1].Value = contact.properties.firstname;
            worksheet.Cells[row, 2].Value = person.NickName;
            if ( person.NickName != person.FirstName )
            {
                worksheet.Cells[row, 2].Value += " (" + person.FirstName + ")";
            }
            //Color cells if they match
            if ( CustomEquals(contact.properties.firstname, person.FirstName) || CustomEquals(contact.properties.firstname, person.NickName) )
            {
                worksheet = ColorCell(worksheet, row, 1);
                worksheet = ColorCell(worksheet, row, 2);
            }

            //Add LastNames
            worksheet.Cells[row, 3].Value = contact.properties.lastname;
            worksheet.Cells[row, 4].Value = person.LastName;
            //Color cells if they match 
            if ( CustomEquals(contact.properties.lastname, person.LastName) )
            {
                worksheet = ColorCell(worksheet, row, 3);
                worksheet = ColorCell(worksheet, row, 4);
            }

            //Add Emails
            worksheet.Cells[row, 5].Value = contact.properties.email;
            worksheet.Cells[row, 6].Value = person.Email;
            //Color cells if they match
            if ( CustomEquals(contact.properties.email, person.Email) )
            {
                worksheet = ColorCell(worksheet, row, 5);
                worksheet = ColorCell(worksheet, row, 6);
            }

            //Add Phone Numbers
            var num = person.PhoneNumbers.FirstOrDefault(pn => pn.Number == contact.properties.phone);
            worksheet.Cells[row, 7].Value = contact.properties.phone;
            worksheet.Cells[row, 8].Value = num != null ? num.Number : "No Matching Number";
            //Color cells if they match
            if ( num != null && CustomEquals(contact.properties.phone, num.Number) )
            {
                worksheet = ColorCell(worksheet, row, 7);
                worksheet = ColorCell(worksheet, row, 8);
            }

            //Add Connection Statuses
            worksheet.Cells[row, 10].Value = person.ConnectionStatusValue;

            //Add links
            worksheet.Cells[row, 11].Value = "https://app.hubspot.com/contacts/6480645/contact/" + contact.id;
            worksheet.Cells[row, 12].Value = "https://rock.lakepointe.church/Person/" + person.Id;

            //Add Created Dates
            if ( !String.IsNullOrEmpty(contact.properties.createdate) )
            {
                DateTime hubspotVal;
                if ( DateTime.TryParse(contact.properties.createdate, out hubspotVal) )
                {
                    worksheet.Cells[row, 13].Value = hubspotVal.ToString("MM/dd/yyyy");
                }
            }
            worksheet.Cells[row, 14].Value = person.CreatedDateTime.Value.ToString("MM/dd/yyyy");

            //Add Modified Dates
            if ( !String.IsNullOrEmpty(contact.properties.lastmodifieddate) )
            {
                DateTime hubspotVal;
                if ( DateTime.TryParse(contact.properties.lastmodifieddate, out hubspotVal) )
                {
                    worksheet.Cells[row, 15].Value = hubspotVal.ToString("MM/dd/yyyy");
                }
            }
            worksheet.Cells[row, 16].Value = person.ModifiedDateTime.Value.ToString("MM/dd/yyyy");

            //Add Rock Id
            worksheet.Cells[row, 17].Value = person.Id;


            return worksheet;
        }

        private bool CustomEquals(string a, string b)
        {
            if ( !String.IsNullOrEmpty(a) && !String.IsNullOrEmpty(b) )
            {
                return a.ToLower() == b.ToLower();
            }
            return false;
        }

    }

    [DebuggerDisplay("Label: {label}, FieldType: {fieldType}")]
    public class HubspotProperty
    {
        public string name { get; set; }
        public string label { get; set; }
        public string fieldType { get; set; }
        public string groupName { get; set; }
    }

    public class HubspotPropertyUpdate
    {
        public string property { get; set; }
        public string value { get; set; }
    }

    public class HSContactProperties
    {
        public string createdate { get; set; }
        public string email { get; set; }
        public string firstname { get; set; }
        public string lastname { get; set; }
        public string lastmodifieddate { get; set; }
        private string _phone { get; set; }
        public string phone
        {
            get
            {
                return !String.IsNullOrEmpty(_phone) ? _phone.Replace(" ", "").Replace("(", "").Replace(")", "").Replace("-", "") : "";
            }
            set
            {
                _phone = value;
            }
        }
        public string rock_person_id { get; set; }

        public string rock_record_status { get; set; }

        public string has_potential_rock_match { get; set; }

        public override string ToString()
        {
            return "CreatedDate: " + createdate + "; LastModifiedDate: " + lastmodifieddate + "; First: " + firstname + "; Last:" + lastname + "; Email: " + email + "; Phone: " + phone + "; RockId: " + rock_person_id + "; RockStatus: " + rock_record_status + "; RockPotentialMatch: " + has_potential_rock_match;
        }
    }

    [DebuggerDisplay("Id: {id}, Email: {properties.email}")]
    public class HSContactResult
    {
        public string id { get; set; }
        public HSContactProperties properties { get; set; }
        public string archived { get; set; }

        public override string ToString()
        {
            return "Id: " + id + "; Properties: " + properties.ToString();
        }
    }
    public class HSResultPaging
    {
        public HSResultPagingNext next { get; set; }
    }
    public class HSResultPagingNext
    {
        public string link { get; set; }
    }
    public class HSContactQueryResult
    {
        public List<HSContactResult> results { get; set; }
        public HSResultPaging paging { get; set; }

        public override string ToString()
        {
            string ret = "";
            foreach ( var item in results )
            {
                ret += "; " + item.ToString();
            }
            return ret;

        }
    }
    public class HSPropertyQueryResult
    {
        public List<HubspotProperty> results { get; set; }
    }
}
