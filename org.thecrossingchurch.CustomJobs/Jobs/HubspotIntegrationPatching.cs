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
using System.Linq;
using Quartz;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;
using Newtonsoft.Json;
using System.Net;
using System.Reflection;
using RestSharp;
using Rock.Security;
using System.ComponentModel;
using System.Threading;
using System.Diagnostics;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using Newtonsoft.Json.Converters;
using OfficeOpenXml.FormulaParsing.Excel.Functions.DateTime;
using System.Data.Entity;
using Rock.Search.Person;
using Quartz.Impl.Matchers;

namespace org.crossingchurch.HubspotIntegration.Jobs
{
    /// <summary>
    /// Job to supply hubspot contacts that already have rock_person_ids with other info.
    /// </summary>
    [DisplayName("Hubspot Integration: Update Records")]
    [Description("This job only updates Hubspot contacts with a valid Rock ID with additional info from Rock.")]
    [DisallowConcurrentExecution]

    [TextField("AttributeKey", "The attribute key for the global attribute that contains the HubSpot API Key. The attribute must be encrypted.", true, "HubspotAPIKeyGlobal")]
    [TextField("Business Unit", "Hubspot Business Unit value", true, "0")]
    [DefinedValueField("Contribution Transaction Type",
        AllowMultiple = false,
        AllowAddingNewValues = false,
        DefaultValue = Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION,
        DefinedTypeGuid = Rock.SystemGuid.DefinedType.FINANCIAL_TRANSACTION_TYPE
    )]
    [BooleanField("Include TMBT", defaultValue: false)]
    [AccountField("Financial Account", "If syncing a total amount given which fund should we sync from")]
    public class HubspotIntegrationPatching : IJob
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
        public HubspotIntegrationPatching()
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

            //Bearer Token, but I didn't change the Attribute Key
            string attrKey = dataMap.GetString("AttributeKey");
            Debug.WriteLine("AttributeKey: " + attrKey);
            string attrValue = GlobalAttributesCache.Get().GetValue(attrKey);
            Debug.WriteLine("AttributeValue: " + attrValue);
            key = "";
            key = Encryption.DecryptString( attrValue );
            Debug.WriteLine("Found key: " + key);
            businessUnit = dataMap.GetString("BusinessUnit");
            var current_id = 0;

            //Bearer Token, but I didn't change the Attribute Key ---- Original. Commented 6/25
            //string attrKey = dataMap.GetString("AttributeKey");
            //key = Encryption.DecryptString(GlobalAttributesCache.Get().GetValue(attrKey));
            //businessUnit = dataMap.GetString("BusinessUnit");

            //var current_id = 0;

            PersonService personService = new PersonService(new RockContext());

            //Get Hubspot Properties in Rock Information Group
            //This will allow us to add properties temporarily to the sync and then not continue to have them forever
            var propClient = new RestClient("https://api.hubapi.com/crm/v3/properties/contacts?properties=name,label,createdUserId,groupName,options,fieldType");
            propClient.Timeout = -1;
            var propRequest = new RestRequest(Method.GET);
            propRequest.AddHeader("Authorization", $"Bearer {key}");
            IRestResponse propResponse = propClient.Execute(propRequest);
            var props = new List<HubspotProperty>();
            var tmbtProps = new List<HubspotProperty>();
            var propsQry = JsonConvert.DeserializeObject<HSPropertyQueryResult>(propResponse.Content);
            props = propsQry.results;

            //Filter to props in Rock Information Group (and Contact information group)
            props = props.Where(p => p.groupName == "rock_information" || p.groupName == "Contact information").ToList();
            //props = props.Where( p => p.groupName == "Contact information" ).ToList(); // - 6/26 sdlp - To update names, address, and phone
            //tmbtProps = propsQry.results.Where(p => p.groupName == "rock_tmbt_information").ToList(); // ---------- Commented 6/25 sdlp
            //Business Unit hs_all_assigned_business_unit_ids
            //Save a list of the ones that are Rock attributes
            //var attrs = props.Where(p => p.label.Contains("Rock Attribute ")).ToList(); // commented sdlp 7/15/24
            RockContext _context = new RockContext();
            //List<string> attrKeys = attrs.Select(hs => hs.label.Replace("Rock Attribute ", "")).ToList(); // commented sdlp 7/15/24
            //var rockAttributes = new AttributeService(_context).Queryable().Where(a => a.EntityTypeId == 15 && attrKeys.Contains(a.Key)); //commented sdlp 7/15/24

            Guid transactionTypeGuid = dataMap.GetString("ContributionTransactionType").AsGuid();
            var transactionTypeDefinedValue = new DefinedValueService(_context).Get(transactionTypeGuid);
            int transactionTypeValueId = transactionTypeDefinedValue.Id;

            //Get List of all contacts from Hubspot
            contacts = new List<HSContactResult>();
            request_count = 0;

            // TODO get all properties...
            GetContacts("https://api.hubapi.com/crm/v3/objects/contacts?limit=100&properties=email,firstname,lastname,gender,birthday,first_time_giving,first_visit_date,baptism,phone,rock_person_id,life_groups,enews_subscriber,first_time_serving,rock_account_created_date,lastmodifieddate");
            Debug.WriteLine("Contacts returned: " + contacts.Count());
            WriteToLog(string.Format("Total Contacts to Match: {0}", contacts.Count()));

            PersonAliasService pa_svc = new PersonAliasService(_context);
            FinancialTransactionService ft_svc = new FinancialTransactionService(_context);
            AttributeValueService av_svc = new AttributeValueService(_context);
            var dataViewService = new DataViewService( _context );
            var enewsDV = dataViewService.Get( "e4f1db79-63c7-41ca-ab45-6ed6b16feb0e" ); // ENews DataView Id: 2882, Guid: e4f1db79-63c7-41ca-ab45-6ed6b16feb0e

            // ENews from DataView To List
            var qry = enewsDV.GetQuery();
            var eNewsData = qry.Select( item => item ).ToList();
            HashSet<string> eNewsEmails = new HashSet<string>();
            //string eNewsEmailList = "";
            foreach ( var row in eNewsData )
            {
                string colVal = row.GetPropertyValue( "Email" ).ToStringSafe().ToLower();
                if ( colVal != "" )
                {
                    eNewsEmails.Add( colVal );
                    //eNewsEmailList = eNewsEmailList + "," + colVal;
                }
            }
            Debug.WriteLine( "eNews Email List: " + eNewsEmails );




            //WriteToLog( string.Format( "Total Contacts: {0}", contacts.Count() ) );
            for ( var i = 0; i < contacts.Count(); i++ )
            {
                //Stopwatch watch = new Stopwatch();
                //watch.Start();
                Person person = personService.Get(contacts[i].properties.rock_person_id);

                //For Testing
                //WriteToLog( string.Format( "{1}i: {0}{1}", i, Environment.NewLine ) );
                //WriteToLog( string.Format( "    After SQL: {0}{1}", watch.ElapsedMilliseconds, Environment.NewLine ) );

                // If person is null, that means that we have a person in HS w/ a personId that no longer exists in rock
                // This implies that a merge occurred and we need to look at the alias table to figure out what the new Id is and update it
                // After updating the ID, we need to find the person object and handle patching

                // Setup for patching
                // Look up hubspot defined type
                DefinedTypeService definedTypeService = new DefinedTypeService( _context );
                DefinedType hsDefinedType = definedTypeService.Get( 527 ); // as of 7/8/24 Dev is 528, Train is 527
                AttributeValueService attributeValueService = new AttributeValueService( _context );
                AttributeService attributeService = new AttributeService( _context );


                //Schedule HubSpot update if 1:1 match
                if ( person != null )
                {
                    current_id = person.Id;
                    var url = $"https://api.hubapi.com/crm/v3/objects/contacts/{contacts[i].id}";
                    Debug.WriteLine( "URL: " + url );
                    var properties = new List<HubspotPropertyUpdate>();


                    foreach ( DefinedValue hsSyncDv in hsDefinedType.DefinedValues )
                    {
                        hsSyncDv.LoadAttributes();
                        Dictionary<string, AttributeValueCache> dvAttributes = hsSyncDv.AttributeValues;
                        
                        string propertyOrAttribute = dvAttributes.GetValueOrNull("IsPropertyOrAttribute").Value;
                        string hsKey = dvAttributes.GetValueOrNull("HubSpotAttributeKey").Value;
                        string type = dvAttributes.GetValueOrNull( "Type" ).Value;
                        string key = hsSyncDv.Value;
                        var value = "";

                        // Get person property or attribute
                        if ( propertyOrAttribute == "Property" ) // is property
                        {
                            value = person.GetPropertyValue(key).ToStringSafe();
                        }
                        else // is attribute
                        {
                            var attributeQry = attributeService.Queryable().Where( a => a.EntityTypeId == 15 && a.Key == key ).AsNoTracking();
                            try
                            {
                                Debug.WriteLine( "Type/Key: " + " " + type + " / " + hsKey );
                                value = attributeValueService.Queryable().Where( av => av.EntityId == current_id ).Join( attributeQry, av => av.AttributeId, a => a.Id, ( av, a ) => av ).Select( av => av.Value ).AsNoTracking().FirstOrDefault().ToStringSafe();
                            }
                            catch
                            {
                                value = "";
                            }
                        }

                        // Set date values to HubSpot required format
                        if ( type == "Date" )
                        { 
                            value = value != "" ? ConvertDate( ( DateTime ) value.AsDateTime() ) : value;
                        }

                        // Patch it!
                        Debug.WriteLine( "Patching: " + hsKey + " " + value );
                        properties.Add( new HubspotPropertyUpdate() { property = hsKey, value = value } );

                    }



                    // Handle Email and Phone
                    PhoneNumber mobile = person.PhoneNumbers.FirstOrDefault( n => n.NumberTypeValueId == 12 );
                    if ( mobile != null && !mobile.IsUnlisted && mobile.IsMessagingEnabled )
                    {
                        properties.Add( new HubspotPropertyUpdate() { property = "phone", value = mobile.NumberFormatted } );
                    }
                    else
                    {
                        properties.Add( new HubspotPropertyUpdate() { property = "phone", value = "" } );
                    }

                    string email = person.Email;
                    if ( person.CanReceiveEmail( true ) )
                    {
                        properties.Add( new HubspotPropertyUpdate() { property = "email", value = email } );
                    }
                    else
                    {
                        properties.Add( new HubspotPropertyUpdate() { property = "email", value = "" } );
                    }

                    // eNews Subscriber true or false
                    string eNewsSub = ( person.Email != "" && person.Email != null && eNewsEmails.Contains( person.Email.ToLower() ) ) ? "true" : "false";
                    properties.Add( new HubspotPropertyUpdate() { property = "enews_subscriber", value = eNewsSub } );
                    Debug.WriteLine( "Patching: eNews: " + eNewsSub + " | " + person.Email );

                    // Discpleship Step Path step completed date gathering
                    Dictionary<int, string> stepsPath = new Dictionary<int, string>()
                    {
                        { 26, "baptism" },
                        { 27, "life_groups" },
                        { 29, "first_time_serving" }
                    };
                    foreach ( KeyValuePair<int, string> kvp in stepsPath )
                    {
                        var stepQuery = from s in _context.Steps
                                       join pa in _context.PersonAliases on s.PersonAliasId equals pa.Id
                                       where s.StepTypeId == kvp.Key && pa.Id == s.PersonAliasId && pa.PersonId == person.Id
                                       select s.CompletedDateTime;
                        var stepResult = stepQuery.FirstOrDefault().ToStringSafe();
                        stepResult = stepResult != "" ? ConvertDate( ( DateTime ) stepResult.AsDateTime() ) : stepResult;
                        Debug.WriteLine( "Discpleship Step Path : " + kvp.Value + " | completed date: " + stepResult );
                        properties.Add( new HubspotPropertyUpdate() { property = kvp.Value, value = stepResult } );
                    }

                    //// First time Next Step Class Scheduled
                    //var nscQuery = from h in _context.Histories
                    //               where h.ValueName == "Next Step Class Scheduled" && h.EntityId == person.Id
                    //               orderby h.Id ascending
                    //               select h.NewValue;
                    //var nscResult = nscQuery.FirstOrDefault().ToStringSafe();
                    //properties.Add( new HubspotPropertyUpdate() { property = "next_steps_class_registration", value = nscResult } );
                    //Debug.WriteLine( "First Time Next Step Class Scheduled : " + nscResult );


                    try
                    {
                        //Update the Contact in Hubspot
                        MakeRequest(current_id, url, properties, 0);
                        //WriteToLog( string.Format( "    After Request: {0}", watch.ElapsedMilliseconds ) );
                    }
                    catch ( Exception err )
                    {
                        ExceptionLogService.LogException(new Exception($"Hubspot Sync Error{Environment.NewLine}{err}{Environment.NewLine}Current Id: {current_id}{Environment.NewLine}Exception from Job:{Environment.NewLine}{err.Message}{Environment.NewLine}"));
                    }
                }
                //WriteToLog( string.Format( "    End of iteration: {0}", watch.ElapsedMilliseconds ) );
                //watch.Stop();
            }
        }

        private void MakeRequest(int current_id, string url, List<HubspotPropertyUpdate> properties, int attempt)
        {
            //Update the Hubspot Contact
            try
            {
                //For Testing Write to Log File
                WriteToLog( string.Format( "{0}     ID: {1}{2}PROPS:{2}{3}", RockDateTime.Now.ToString( "HH:mm:ss.ffffff" ), current_id, Environment.NewLine, JsonConvert.SerializeObject( properties ) ) );

                var client = new RestClient(url);
                client.Timeout = -1;
                var request = new RestRequest(Method.PATCH);
                request.AddHeader("accept", "application/json");
                request.AddHeader("content-type", "application/json");
                request.AddHeader("Authorization", $"Bearer {key}");
                request.AddParameter( "application/json", $"{{\"properties\": {{ {String.Join( ",", properties.Select( p => $"\"{p.property}\": \"{p.value}\"" ) )} }} }}", ParameterType.RequestBody );
                IRestResponse response = client.Execute(request);
                if ( ( int )response.StatusCode == 429 )
                {
                    if ( attempt < 3 )
                    {
                        Thread.Sleep(9000);
                        MakeRequest(current_id, url, properties, attempt + 1);
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
            string logFile = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Logs/HubSpotPatchLog.txt");
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
            contacts.AddRange(contactResults.results.Where(c => c.properties.rock_person_id != null && c.properties.rock_person_id != "" && c.properties.email != null && c.properties.email != "").ToList());
            if ( contactResults.paging != null && contactResults.paging.next != null && !String.IsNullOrEmpty(contactResults.paging.next.link) && request_count < 500 )
            {
                GetContacts(contactResults.paging.next.link);
            }
        }

        private string ConvertDate(DateTime? date)
        {
            if ( date.HasValue )
            {
                DateTime today = RockDateTime.Now;
                if ( today.Year - date.Value.Year < 1000 && today.Year - date.Value.Year > -1000 )
                {
                    date = new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, 0, 0, 0);
                    var d = date.Value.Subtract(new DateTime(1970, 1, 1)).TotalSeconds * 1000;
                    return d.ToString();
                }
            }
            return "";
        }
    }
}
