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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Runtime.Serialization;
using Rock.Data;
using Rock.Web.Cache;
using Rock.Lava;

namespace Rock.Model
{
    /// <summary>
    /// Represents a connection type
    /// </summary>
    [RockDomain( "Engagement" )]
    [Table( "ConnectionType" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( Rock.SystemGuid.EntityType.CONNECTION_TYPE )]
    public partial class ConnectionType : Model<ConnectionType>, IOrdered, ICacheable
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        [Required]
        [MaxLength( 50 )]
        [DataMember( IsRequired = true )]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>
        /// The description.
        /// </value>
        [DataMember]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the icon CSS class.
        /// </summary>
        /// <value>
        /// The icon CSS class.
        /// </value>
        [MaxLength( 100 )]
        [DataMember]
        public string IconCssClass { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether future follow-ups are enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if future follow-ups are enabled; otherwise, <c>false</c>.
        /// </value>
        [Required]
        [DataMember]
        public bool EnableFutureFollowup { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether full activity lists are enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if full activity lists are enabled; otherwise, <c>false</c>.
        /// </value>
        [Required]
        [DataMember]
        public bool EnableFullActivityList { get; set; }


        /// <summary>
        /// Gets or sets a value indicating whether this connection type requires a placement group to connect.
        /// </summary>
        /// <value>
        /// <c>true</c> if connection type requires a placement group to connect; otherwise, <c>false</c>.
        /// </value>
        [Required]
        [DataMember]
        public bool RequiresPlacementGroupToConnect { get; set; }

        /// <summary>
        /// Gets or sets the owner <see cref="Rock.Model.PersonAlias"/> identifier.
        /// </summary>
        /// <value>
        /// The owner person alias identifier.
        /// </value>
        [DataMember]
        public int? OwnerPersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the number of days until the request is considered idle.
        /// </summary>
        /// <value>
        /// This determines how many days can pass before the request is considered idle.
        /// </value>
        [DataMember]
        public int DaysUntilRequestIdle { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is active.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is active; otherwise, <c>false</c>.
        /// </value>
        [Required]
        [DataMember]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether [enable request security].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [enable request security]; otherwise, <c>false</c>.
        /// </value>
        [Required]
        public bool EnableRequestSecurity { get; set; }

        /// <summary>
        /// Gets or sets the connection request detail <see cref="Rock.Model.Page"/> identifier.
        /// </summary>
        /// <value>
        /// The connection request detail page identifier.
        /// </value>
        [DataMember]
        public int? ConnectionRequestDetailPageId { get; set; }

        /// <summary>
        /// Gets or sets the connection request detail <see cref="Rock.Model.PageRoute"/> identifier.
        /// </summary>
        /// <value>
        /// The connection request detail page route identifier.
        /// </value>
        [DataMember]

        public int? ConnectionRequestDetailPageRouteId { get; set; }

        /// <summary>
        /// Gets or sets the default view mode (list or board).
        /// </summary>
        /// <value>
        /// The default view.
        /// </value>
        [DataMember]
        public ConnectionTypeViewMode DefaultView { get; set; }

        /// <summary>
        /// Gets or sets the request header lava.
        /// </summary>
        /// <value>
        /// The request header lava.
        /// </value>
        [DataMember]
        public string RequestHeaderLava { get; set; }

        /// <summary>
        /// Gets or sets the request badge lava.
        /// </summary>
        /// <value>
        /// The request badge lava.
        /// </value>
        [DataMember]
        public string RequestBadgeLava { get; set; }

        /// <summary>
        /// Gets or sets the order.
        /// </summary>
        /// <value>
        /// The order.
        /// </value>
        [DataMember]
        public int Order { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the owner <see cref="Rock.Model.PersonAlias"/>.
        /// </summary>
        /// <value>
        /// The owner person alias.
        /// </value>
        [LavaVisible]
        public virtual PersonAlias OwnerPersonAlias { get; set; }

        /// <summary>
        /// Gets or sets the connection request detail <see cref="Rock.Model.Page"/>.
        /// </summary>
        /// <value>
        /// The connection request detail page.
        /// </value>
        [LavaVisible]
        public virtual Page ConnectionRequestDetailPage { get; set; }

        /// <summary>
        /// Gets or sets the connection request detail <see cref="Rock.Model.PageRoute"/>.
        /// </summary>
        /// <value>
        /// The connection request detail page route.
        /// </value>
        [DataMember]
        public virtual PageRoute ConnectionRequestDetailPageRoute { get; set; }

        /// <summary>
        /// Gets or sets a collection containing the <see cref="Rock.Model.ConnectionStatus">ConnectionStatuses</see> who are associated with the ConnectionType.
        /// </summary>
        /// <value>
        /// A collection of <see cref="Rock.Model.ConnectionStatus">ConnectionStatuses</see> who are associated with the ConnectionType.
        /// </value>
        [LavaVisible]
        public virtual ICollection<ConnectionStatus> ConnectionStatuses
        {
            get { return _connectionStatuses ?? ( _connectionStatuses = new Collection<ConnectionStatus>() ); }
            set { _connectionStatuses = value; }
        }

        private ICollection<ConnectionStatus> _connectionStatuses;

        /// <summary>
        /// Gets or sets a collection containing the <see cref="Rock.Model.ConnectionWorkflow">ConnectionWorkflows</see> who are associated with the ConnectionType.
        /// </summary>
        /// <value>
        /// A collection of <see cref="Rock.Model.ConnectionWorkflow">ConnectionWorkflows</see> who are associated with the ConnectionType.
        /// </value>
        [LavaVisible]
        public virtual ICollection<ConnectionWorkflow> ConnectionWorkflows
        {
            get { return _connectionWorkflows ?? ( _connectionWorkflows = new Collection<ConnectionWorkflow>() ); }
            set { _connectionWorkflows = value; }
        }

        private ICollection<ConnectionWorkflow> _connectionWorkflows;

        /// <summary>
        /// Gets or sets a collection containing the <see cref="Rock.Model.ConnectionActivityType">ConnectionActivityTypes</see> who are associated with the ConnectionType.
        /// </summary>
        /// <value>
        /// A collection of <see cref="Rock.Model.ConnectionActivityType">ConnectionActivityTypes</see> who are associated with the ConnectionType.
        /// </value>
        [LavaVisible]
        public virtual ICollection<ConnectionActivityType> ConnectionActivityTypes
        {
            get { return _connectionActivityTypes ?? ( _connectionActivityTypes = new Collection<ConnectionActivityType>() ); }
            set { _connectionActivityTypes = value; }
        }

        private ICollection<ConnectionActivityType> _connectionActivityTypes;

        /// <summary>
        /// Gets or sets a collection containing the <see cref="Rock.Model.ConnectionOpportunity">ConnectionOpportunities</see> who are associated with the ConnectionType.
        /// </summary>
        /// <value>
        /// A collection of <see cref="Rock.Model.ConnectionOpportunity">ConnectionOpportunities</see> who are associated with the ConnectionType.
        /// </value>
        [LavaVisible]
        public virtual ICollection<ConnectionOpportunity> ConnectionOpportunities
        {
            get { return _connectionOpportunities ?? ( _connectionOpportunities = new Collection<ConnectionOpportunity>() ); }
            set { _connectionOpportunities = value; }
        }

        private ICollection<ConnectionOpportunity> _connectionOpportunities;

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return this.Name;
        }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// ConnectionType Configuration class.
    /// </summary>
    public partial class ConnectionTypeConfiguration : EntityTypeConfiguration<ConnectionType>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionTypeConfiguration" /> class.
        /// </summary>
        public ConnectionTypeConfiguration()
        {
            this.HasOptional( p => p.OwnerPersonAlias ).WithMany().HasForeignKey( p => p.OwnerPersonAliasId ).WillCascadeOnDelete( false );
            this.HasOptional( p => p.ConnectionRequestDetailPage ).WithMany().HasForeignKey( p => p.ConnectionRequestDetailPageId ).WillCascadeOnDelete( false );
            this.HasOptional( p => p.ConnectionRequestDetailPageRoute ).WithMany().HasForeignKey( p => p.ConnectionRequestDetailPageRouteId ).WillCascadeOnDelete( false );
        }
    }

    #endregion Entity Configuration
}