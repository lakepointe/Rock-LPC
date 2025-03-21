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
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;
using Rock.Data;
using Rock.Tasks;
using Rock.Utility;
using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Represents a Streak Type Exclusion in Rock.
    /// </summary>
    [RockDomain( "Engagement" )]
    [Table( "StreakTypeExclusion" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( "1F00C782-F8A2-4CFA-B7DF-E5B3B6D36069")]
    public partial class StreakTypeExclusion : Model<StreakTypeExclusion>, ICacheable
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the Id of the <see cref="Rock.Model.StreakType"/> to which this exclusion map belongs. This property is required.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int StreakTypeId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.Location"/>  identifier by which the streak type's exclusions will be associated.
        /// </summary>
        [DataMember]
        public int? LocationId { get; set; }

        /// <summary>
        /// The sequence of bits that represent exclusions. The least significant bit is representative of the Streak Type's StartDate.
        /// More significant bits (going left) are more recent dates.
        /// </summary>
        [DataMember]
        [CodeGenExclude( CodeGenFeature.ViewModelFile )]
        public byte[] ExclusionMap { get; set; }

        #endregion Entity Properties

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the Sequence <see cref="Rock.Model.StreakType"/> .
        /// </summary>
        [DataMember]
        public virtual StreakType StreakType { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.Location"/> .
        /// </summary>
        [DataMember]
        public virtual Location Location { get; set; }

        #endregion Navigation Properties

        #region Entity Configuration

        /// <summary>
        /// Streak Type Exclusion Configuration class.
        /// </summary>
        public partial class StreakTypeExclusionConfiguration : EntityTypeConfiguration<StreakTypeExclusion>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="StreakTypeExclusionConfiguration"/> class.
            /// </summary>
            public StreakTypeExclusionConfiguration()
            {
                HasRequired( soe => soe.StreakType ).WithMany( s => s.StreakTypeExclusions ).HasForeignKey( soe => soe.StreakTypeId ).WillCascadeOnDelete( true );

                HasOptional( se => se.Location ).WithMany().HasForeignKey( se => se.LocationId ).WillCascadeOnDelete( false );
            }
        }

        #endregion Entity Configuration

        #region Update Hook

        /// <summary>
        /// Perform tasks prior to saving changes to this entity.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="entry">The entry.</param>
        public override void PreSaveChanges( Data.DbContext dbContext, DbEntityEntry entry )
        {
            // Add a bus to process denormalized data refreshes
            var processStreakTypeExclusionChangeMsg = GetProcessStreakTypeExclusionChangeMsg( entry );
            processStreakTypeExclusionChangeMsg.Send();
            base.PreSaveChanges( dbContext, entry );
        }

        #endregion Update Hook

        #region Private Methods

        /// <summary>
        /// Get ProcessStreakTypeExclusionChange Message.
        /// </summary>
        /// <param name="entry">The entry.</param>
        private ProcessStreakTypeExclusionChange.Message GetProcessStreakTypeExclusionChangeMsg( DbEntityEntry entry )
        {
            var processStreakTypeExclusionChangeMsg = new ProcessStreakTypeExclusionChange.Message();
            var isAdded = entry.State == EntityState.Added;
            var mapIsModified = entry.State == EntityState.Modified && entry.Property( "ExclusionMap" )?.IsModified == true;

            if ( !isAdded && !mapIsModified )
            {
                return processStreakTypeExclusionChangeMsg;
            }

            var streakTypeExclusion = entry.Entity as StreakTypeExclusion;
            processStreakTypeExclusionChangeMsg.StreakTypeId = streakTypeExclusion?.StreakTypeId ?? default;
            return processStreakTypeExclusionChangeMsg;
        }

        #endregion Private Methods
    }
}
