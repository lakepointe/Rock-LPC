﻿// <copyright>
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
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Entity;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Rock.Jobs
{
    /// <summary>
    /// Job that populates Rock's family analytics.
    /// </summary>
    [DisplayName( "Family Analytics" )]
    [Description( "Job that populates Rock's family analytics." )]

    [WorkflowTypeField( "eRA Entry Workflow", "The workflow type to launch when a family becomes an eRA.", key: "EraEntryWorkflow", order: 0)]
    [WorkflowTypeField( "eRA Exit Workflow", "The workflow type to launch when a family exits from being an eRA.", key: "EraExitWorkflow", order: 1 )]

    [BooleanField("Set Visit Dates", "If enabled will update the first and second visit person attributes.", true, order: 3)]

    [IntegerField( "Command Timeout", "Maximum amount of time (in seconds) to wait for the sql operations to complete. Leave blank to use the default for this job (3600). Note, some operations could take several minutes, so you might want to set it at 3600 (60 minutes) or higher", false, 60 * 60, "General", 1, "CommandTimeout" )]
    public class CalculateFamilyAnalytics : RockJob
    {
        private const string SOURCE_OF_CHANGE = "Calculate Family Analytics Job";

        /// <summary> 
        /// Empty constructor for job initialization
        /// <para>
        /// Jobs require a public empty constructor so that the
        /// scheduler can instantiate the class whenever it needs.
        /// </para>
        /// </summary>
        public CalculateFamilyAnalytics()
        {
        }

        /// <summary>
        /// Job that will run quick SQL queries on a schedule.
        /// </summary>
        public override void Execute()
        {
            Guid? entryWorkflowType = GetAttributeValue( "EraEntryWorkflow" ).AsGuidOrNull();
            Guid? exitWorkflowType = GetAttributeValue( "EraExitWorkflow" ).AsGuidOrNull();
            bool updateVisitDates = GetAttributeValue( "SetVisitDates" ).AsBoolean();

            int commandTimeout = GetAttributeValue( "CommandTimeout" ).AsIntegerOrNull() ?? 3600;

            // configuration
            //

            // giving
            int exitGivingCount = 1;

            // attendance
            int exitAttendanceCountShort = 1;
            int exitAttendanceCountLong = 8;

            // get era dataset from stored proc
            var resultContext = new RockContext();
            
            var eraAttribute = AttributeCache.Get( SystemGuid.Attribute.PERSON_ERA_CURRENTLY_AN_ERA.AsGuid() );
            var eraStartAttribute = AttributeCache.Get( SystemGuid.Attribute.PERSON_ERA_START_DATE.AsGuid() );
            var eraEndAttribute = AttributeCache.Get( SystemGuid.Attribute.PERSON_ERA_END_DATE.AsGuid() );

            if (eraAttribute == null || eraStartAttribute == null || eraEndAttribute == null)
            {
                throw new Exception( "Family analytic attributes could not be found" );
            }

            resultContext.Database.CommandTimeout = commandTimeout;

            this.UpdateLastStatusMessage( "Getting Family Analytics Era Dataset..." );

            var results = resultContext.Database.SqlQuery<EraResult>( "spCrm_FamilyAnalyticsEraDataset" ).ToList();

            int personEntityTypeId = EntityTypeCache.Get( "Rock.Model.Person" ).Id;
            int attributeEntityTypeId = EntityTypeCache.Get( "Rock.Model.Attribute" ).Id;
            int eraAttributeId = eraAttribute.Id;
            int personAnalyticsCategoryId = CategoryCache.Get( SystemGuid.Category.HISTORY_PERSON_ANALYTICS.AsGuid() ).Id;

            int progressPosition = 0;
            int progressTotal = results.Count;

            foreach (var result in results )
            {
                progressPosition++;
                // create new rock context for each family (https://weblog.west-wind.com/posts/2014/Dec/21/Gotcha-Entity-Framework-gets-slow-in-long-Iteration-Loops)
                RockContext updateContext = new RockContext();
                updateContext.SourceOfChange = SOURCE_OF_CHANGE;
                updateContext.Database.CommandTimeout = commandTimeout;
                var attributeValueService = new AttributeValueService( updateContext );
                var historyService = new HistoryService( updateContext );

                // if era ensure it still meets requirements
                if ( result.IsEra )
                {
                    // This process will not remove eRA status from a single inactive family member if the family is considered eRA, even if the person record status is inactive.
                    // It removes eRA status from all family members if the family no longer meets the eRA requirements.
                    if (result.ExitGiftCountDuration < exitGivingCount && result.ExitAttendanceCountDurationShort < exitAttendanceCountShort && result.ExitAttendanceCountDurationLong < exitAttendanceCountLong )
                    {
                        // exit era (delete attribute value from each person in family)
                        var family = new GroupService( updateContext ).Queryable( "Members, Members.Person" ).AsNoTracking().Where( m => m.Id == result.FamilyId ).FirstOrDefault();

                        if ( family != null ) {
                            foreach ( var person in family.Members.Select( m => m.Person ) ) {

                                // remove the era flag
                                var eraAttributeValue = attributeValueService.Queryable().Where( v => v.AttributeId == eraAttribute.Id && v.EntityId == person.Id ).FirstOrDefault();
                                if ( eraAttributeValue != null )
                                {
                                    attributeValueService.Delete( eraAttributeValue );
                                }

                                // set end date
                                var eraEndAttributeValue = attributeValueService.Queryable().Where( v => v.AttributeId == eraEndAttribute.Id && v.EntityId == person.Id ).FirstOrDefault();
                                if ( eraEndAttributeValue == null )
                                {
                                    eraEndAttributeValue = new AttributeValue();
                                    eraEndAttributeValue.EntityId = person.Id;
                                    eraEndAttributeValue.AttributeId = eraEndAttribute.Id;
                                    attributeValueService.Add( eraEndAttributeValue );
                                }
                                eraEndAttributeValue.Value = RockDateTime.Now.ToISO8601DateString();

                                // add a history record
                                if ( personAnalyticsCategoryId != 0 && personEntityTypeId != 0 && attributeEntityTypeId != 0 && eraAttributeId != 0 )
                                {
                                    History historyRecord = new History();
                                    historyService.Add( historyRecord );
                                    historyRecord.EntityTypeId = personEntityTypeId;
                                    historyRecord.EntityId = person.Id;
                                    historyRecord.CreatedDateTime = RockDateTime.Now;
                                    historyRecord.CreatedByPersonAliasId = person.PrimaryAliasId;
                                    historyRecord.Caption = "eRA";

                                    historyRecord.Verb = "EXITED";
                                    historyRecord.ChangeType = History.HistoryChangeType.Attribute.ConvertToString();
                                    historyRecord.ValueName = "eRA";
                                    historyRecord.NewValue = "Exited";
                                    
                                    historyRecord.RelatedEntityTypeId = attributeEntityTypeId;
                                    historyRecord.RelatedEntityId = eraAttributeId;
                                    historyRecord.CategoryId = personAnalyticsCategoryId;
                                    historyRecord.SourceOfChange = SOURCE_OF_CHANGE;
                                }

                                updateContext.SaveChanges();
                            }

                            // launch exit workflow
                            if ( exitWorkflowType.HasValue )
                            {
                                LaunchWorkflow( exitWorkflowType.Value, family );
                            }
                        }
                    }
                }
                else
                {
                    // entered era
                    var family = new GroupService( updateContext ).Queryable( "Members" ).AsNoTracking().Where( m => m.Id == result.FamilyId ).FirstOrDefault();

                    if ( family != null )
                    {
                        // The stored procedure does not filter out inactive users because we want to exit the family from eRA if they are not active.
                        // So check the status for each person here and do not add the person if they are inactive. If the system defined value for 
                        // an inactive person is not available then use "-1" as every record should pass != -1.
                        int inactiveStatusId = DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE.AsGuid() ) ?? -1;

                        var familyMembers = family.Members
                            .Where( m => !m.Person.IsDeceased )
                            .Where( m => m.Person.RecordStatusValueId != inactiveStatusId )
                            .Select( m => m.Person );

                        foreach ( var person in familyMembers )
                        {
                            // set era attribute to true
                            var eraAttributeValue = attributeValueService.Queryable().Where( v => v.AttributeId == eraAttribute.Id && v.EntityId == person.Id ).FirstOrDefault();
                            var isCurrentlyEra = eraAttributeValue?.ValueAsBoolean ?? false;

                            if ( eraAttributeValue == null )
                            {
                                eraAttributeValue = new AttributeValue();
                                eraAttributeValue.EntityId = person.Id;
                                eraAttributeValue.AttributeId = eraAttribute.Id;
                                attributeValueService.Add( eraAttributeValue );
                            }
                            eraAttributeValue.Value = bool.TrueString;

                            // add start date
                            var eraStartAttributeValue = attributeValueService.Queryable().Where( v => v.AttributeId == eraStartAttribute.Id && v.EntityId == person.Id ).FirstOrDefault();
                            if ( eraStartAttributeValue == null )
                            {
                                eraStartAttributeValue = new AttributeValue();
                                eraStartAttributeValue.EntityId = person.Id;
                                eraStartAttributeValue.AttributeId = eraStartAttribute.Id;
                                attributeValueService.Add( eraStartAttributeValue );
                            }
                            // There can be some circumstances where a child family member may already be eRa whiles the adult family member is not,
                            // thus we check if the current family member is already eRa, if they are their eRa date and history is not updated.
                            eraStartAttributeValue.Value = !isCurrentlyEra || !eraStartAttributeValue.ValueAsDateTime.HasValue ? RockDateTime.Now.ToISO8601DateString() : eraStartAttributeValue.Value;

                            // delete end date if it exists
                            var eraEndAttributeValue = attributeValueService.Queryable().Where( v => v.AttributeId == eraEndAttribute.Id && v.EntityId == person.Id ).FirstOrDefault();
                            if ( eraEndAttributeValue != null )
                            {
                                attributeValueService.Delete( eraEndAttributeValue );
                            }

                            // add a history record
                            if ( personAnalyticsCategoryId != 0 && personEntityTypeId != 0 && attributeEntityTypeId != 0 && eraAttributeId != 0 && !isCurrentlyEra )
                            {
                                History historyRecord = new History();
                                historyService.Add( historyRecord );
                                historyRecord.EntityTypeId = personEntityTypeId;
                                historyRecord.EntityId = person.Id;
                                historyRecord.CreatedDateTime = RockDateTime.Now;
                                historyRecord.CreatedByPersonAliasId = person.PrimaryAliasId;
                                historyRecord.Caption = "eRA";
                                historyRecord.Verb = "ENTERED";
                                historyRecord.ChangeType = History.HistoryChangeType.Attribute.ConvertToString();
                                historyRecord.RelatedEntityTypeId = attributeEntityTypeId;
                                historyRecord.RelatedEntityId = eraAttributeId;
                                historyRecord.CategoryId = personAnalyticsCategoryId;
                                historyRecord.SourceOfChange = SOURCE_OF_CHANGE;
                            }

                            updateContext.SaveChanges();
                        }

                        // launch entry workflow
                        if ( entryWorkflowType.HasValue )
                        {
                            LaunchWorkflow( entryWorkflowType.Value, family );
                        }
                    }
                }

                // update stats
                this.UpdateLastStatusMessage( $"Updating eRA {progressPosition} of {progressTotal}" );
            }

            // load giving attributes
            this.UpdateLastStatusMessage( "Updating Giving..." );
            resultContext.Database.ExecuteSqlCommand( "spCrm_FamilyAnalyticsGiving" );

            // load attendance attributes
            this.UpdateLastStatusMessage( "Updating Attendance..." );
            resultContext.Database.ExecuteSqlCommand( "spCrm_FamilyAnalyticsAttendance" );

            // process visit dates
            if ( updateVisitDates )
            {
                this.UpdateLastStatusMessage( "Updating Visit Dates..." );
                resultContext.Database.ExecuteSqlCommand( "spCrm_FamilyAnalyticsUpdateVisitDates" );
            }

            this.UpdateLastStatusMessage( "" );
        }

        /// <summary>
        /// Launches the workflow.
        /// </summary>
        /// <param name="workflowTypeGuid">The workflow type unique identifier.</param>
        /// <param name="family">The family.</param>
        private void LaunchWorkflow( Guid workflowTypeGuid, Group family )
        {
            int adultRoleId = GroupTypeCache.Get( SystemGuid.GroupType.GROUPTYPE_FAMILY.AsGuid() ).Roles.Where(r => r.Guid == SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT.AsGuid()).FirstOrDefault().Id;

            var headOfHouse = family.Members.Where( m => m.GroupRoleId == adultRoleId ).OrderBy( m => m.Person.Gender ).FirstOrDefault();

            // don't launch a workflow if no adult is present in the family
            if ( headOfHouse != null && headOfHouse.Person != null && headOfHouse.Person.PrimaryAlias != null ) {
                var spouse = family.Members.Where( m => m.GroupRoleId == adultRoleId && m.PersonId != headOfHouse.Person.Id ).FirstOrDefault();

                using ( var rockContext = new RockContext() )
                {
                    var workflowType = WorkflowTypeCache.Get( workflowTypeGuid );
                    if ( workflowType != null && ( workflowType.IsActive ?? true ) )
                    {
                        var workflowService = new WorkflowService( rockContext );
                        var workflow = Rock.Model.Workflow.Activate( workflowType, headOfHouse.Person.FullName, rockContext );
                        workflowService.Add( workflow );
                        rockContext.SaveChanges();

                        workflow.SetAttributeValue( "Family", family.Guid );
                        workflow.SetAttributeValue( "HeadOfHouse", headOfHouse.Person.PrimaryAlias.Guid );

                        if ( family.Campus != null )
                        {
                            workflow.SetAttributeValue( "Campus", family.Campus.Guid );
                        }

                        if ( spouse != null && spouse.Person != null && spouse.Person.PrimaryAlias != null )
                        {
                            workflow.SetAttributeValue( "Spouse", spouse.Person.PrimaryAlias.Guid );
                        }

                        workflow.SaveAttributeValues();
                    }
                }
            }
        }

        /// <summary>
        /// eRA Result Class
        /// </summary>
        public class EraResult
        {            
            /// <summary>
            /// Gets or sets the family identifier.
            /// </summary>
            /// <value>
            /// The family identifier.
            /// </value>
            [Key]
            public int FamilyId { get; set; }
            /// <summary>
            /// Gets or sets the entry gift duration short. Default 6 weeks.
            /// </summary>
            /// <value>
            /// The entry gift duration short.
            /// </value>
            public int EntryGiftCountDurationShort { get; set; }
            /// <summary>
            /// Gets or sets the entry gift duration long. Default 52 weeks.
            /// </summary>
            /// <value>
            /// The entry gift duration long.
            /// </value>
            public int EntryGiftCountDurationLong { get; set; }
            /// <summary>
            /// Gets or sets the duration of the exit gift. Default 8 weeks.
            /// </summary>
            /// <value>
            /// The duration of the exit gift.
            /// </value>
            public int ExitGiftCountDuration { get; set; }
            /// <summary>
            /// Gets or sets the duration of the entry attendance. Default 16 weeks.
            /// </summary>
            /// <value>
            /// The duration of the entry attendance.
            /// </value>
            public int EntryAttendanceCountDuration { get; set; }
            /// <summary>
            /// Gets or sets the exit attendance duration short. Default 4 weeks.
            /// </summary>
            /// <value>
            /// The exit attendance duration short.
            /// </value>
            public int ExitAttendanceCountDurationShort { get; set; }
            /// <summary>
            /// Gets or sets the exit attendance duration long. Default 16 weeks.
            /// </summary>
            /// <value>
            /// The exit attendance duration long.
            /// </value>
            public int ExitAttendanceCountDurationLong { get; set; }
            /// <summary>
            /// Gets or sets a value indicating whether this instance is era.
            /// </summary>
            /// <value>
            ///   <c>true</c> if this instance is era; otherwise, <c>false</c>.
            /// </value>
            public bool IsEra { get; set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="EraResult"/> class.
            /// </summary>
            public EraResult() { }
            
        }
    }
}
