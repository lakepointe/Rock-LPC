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
using Rock.Attribute;
using Rock.Data;
using Rock.Model;

using System.ComponentModel;
using System.Linq;

namespace Rock.Jobs.PostUpdateJobs
{
    /// <summary>
    /// Run once job for v16 to update the TargetCount property on all the
    /// existing AchievementType entities.
    /// </summary>
    [DisplayName( "Rock Update Helper v16.6 - Update AchievementType TargetCount." )]
    [Description( "This job updates all TargetCount values on the AchievementType table to match what they would be after save." )]

    [IntegerField(
        "Command Timeout",
        Key = AttributeKey.CommandTimeout,
        Description = "Maximum amount of time (in seconds) to wait for each SQL command to complete. On a large database this could take several minutes or more.",
        IsRequired = false,
        DefaultIntegerValue = 14400 )]
    public class PostV166UpdateAchievementTypeTargetCount : PostUpdateJobs.PostUpdateJob
    {
        private static class AttributeKey
        {
            public const string CommandTimeout = "CommandTimeout";
        }

        /// <inheritdoc />
        public override void Execute()
        {
            using ( var rockContext = new RockContext() )
            {
                rockContext.Database.CommandTimeout = GetAttributeValue( AttributeKey.CommandTimeout ).AsInteger();

                var service = new AchievementTypeService( rockContext );
                var achievementTypes = service.Queryable().ToList();

                achievementTypes.LoadAttributes( rockContext );

                foreach ( var  achievementType in achievementTypes )
                {
                    achievementType.UpdateTargetCount( rockContext );
                }

                // Disable pre/post processing so that we don't update the
                // ModifiedByPersonAliasId and ModifiedDateTime properties.
                rockContext.SaveChanges( new SaveChangesArgs
                {
                    DisablePrePostProcessing = true
                } );
            }

            DeleteJob();
        }

        /// <summary>
        /// Deletes the job.
        /// </summary>
        private void DeleteJob()
        {
            using ( var rockContext = new RockContext() )
            {
                var jobService = new ServiceJobService( rockContext );
                var job = jobService.Get( GetJobId() );

                if ( job != null )
                {
                    jobService.Delete( job );
                    rockContext.SaveChanges();
                }
            }
        }
    }
}
