//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the Rock.CodeGeneration project
//     Changes to this file will be lost when the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
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

using System;
using System.Linq;

using Rock.Data;

namespace Rock.Model
{
    /// <summary>
    /// LearningActivityCompletion Service class
    /// </summary>
    public partial class LearningActivityCompletionService : Service<LearningActivityCompletion>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LearningActivityCompletionService"/> class
        /// </summary>
        /// <param name="context">The context.</param>
        public LearningActivityCompletionService(RockContext context) : base(context)
        {
        }

        /// <summary>
        /// Determines whether this instance can delete the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>
        ///   <c>true</c> if this instance can delete the specified item; otherwise, <c>false</c>.
        /// </returns>
        public bool CanDelete( LearningActivityCompletion item, out string errorMessage )
        {
            errorMessage = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Generated Extension Methods
    /// </summary>
    public static partial class LearningActivityCompletionExtensionMethods
    {
        /// <summary>
        /// Clones this LearningActivityCompletion object to a new LearningActivityCompletion object
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="deepCopy">if set to <c>true</c> a deep copy is made. If false, only the basic entity properties are copied.</param>
        /// <returns></returns>
        public static LearningActivityCompletion Clone( this LearningActivityCompletion source, bool deepCopy )
        {
            if (deepCopy)
            {
                return source.Clone() as LearningActivityCompletion;
            }
            else
            {
                var target = new LearningActivityCompletion();
                target.CopyPropertiesFrom( source );
                return target;
            }
        }

        /// <summary>
        /// Clones this LearningActivityCompletion object to a new LearningActivityCompletion object with default values for the properties in the Entity and Model base classes.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static LearningActivityCompletion CloneWithoutIdentity( this LearningActivityCompletion source )
        {
            var target = new LearningActivityCompletion();
            target.CopyPropertiesFrom( source );

            target.Id = 0;
            target.Guid = Guid.NewGuid();
            target.ForeignKey = null;
            target.ForeignId = null;
            target.ForeignGuid = null;
            target.CreatedByPersonAliasId = null;
            target.CreatedDateTime = RockDateTime.Now;
            target.ModifiedByPersonAliasId = null;
            target.ModifiedDateTime = RockDateTime.Now;

            return target;
        }

        /// <summary>
        /// Copies the properties from another LearningActivityCompletion object to this LearningActivityCompletion object
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="source">The source.</param>
        public static void CopyPropertiesFrom( this LearningActivityCompletion target, LearningActivityCompletion source )
        {
            target.Id = source.Id;
            target.ActivityComponentCompletionJson = source.ActivityComponentCompletionJson;
            target.AvailableDateTime = source.AvailableDateTime;
            target.BinaryFileId = source.BinaryFileId;
            target.CompletedByPersonAliasId = source.CompletedByPersonAliasId;
            target.CompletedDateTime = source.CompletedDateTime;
            target.DueDate = source.DueDate;
            target.FacilitatorComment = source.FacilitatorComment;
            target.ForeignGuid = source.ForeignGuid;
            target.ForeignKey = source.ForeignKey;
            target.IsFacilitatorCompleted = source.IsFacilitatorCompleted;
            target.IsStudentCompleted = source.IsStudentCompleted;
            target.LearningActivityId = source.LearningActivityId;
            target.NotificationCommunicationId = source.NotificationCommunicationId;
            target.PointsEarned = source.PointsEarned;
            target.StudentComment = source.StudentComment;
            target.StudentId = source.StudentId;
            target.WasCompletedOnTime = source.WasCompletedOnTime;
            target.CreatedDateTime = source.CreatedDateTime;
            target.ModifiedDateTime = source.ModifiedDateTime;
            target.CreatedByPersonAliasId = source.CreatedByPersonAliasId;
            target.ModifiedByPersonAliasId = source.ModifiedByPersonAliasId;
            target.Guid = source.Guid;
            target.ForeignId = source.ForeignId;

        }
    }
}
