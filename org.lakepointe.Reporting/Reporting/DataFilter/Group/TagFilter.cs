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
using System.ComponentModel.Composition;
using System.Linq;
using System.Linq.Expressions;
using System.Web.UI;
using System.Web.UI.WebControls;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Reporting;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace org.lakepointe.Reporting.DataFilter.Group
{
    /// <summary>
    ///
    /// </summary>
    [Description( "Filter groups based on a group tag" )]
    [Export( typeof( DataFilterComponent ) )]
    [ExportMetadata( "ComponentName", "Group Has Tag Filter" )]
    public class TagFilter : DataFilterComponent
    {
        #region Properties

        /// <summary>
        /// Gets the entity type that filter applies to.
        /// </summary>
        /// <value>
        /// The entity that filter applies to.
        /// </value>
        public override string AppliesToEntityType
        {
            get { return "Rock.Model.Group"; }
        }

        /// <summary>
        /// Gets the section.
        /// </summary>
        /// <value>
        /// The section.
        /// </value>
        public override string Section
        {
            get { return "Additional Filters"; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the title.
        /// </summary>
        /// <param name="entityType"></param>
        /// <returns></returns>
        /// <value>
        /// The title.
        /// </value>
        public override string GetTitle( Type entityType )
        {
            return "Group Tag";
        }

        /// <summary>
        /// Formats the selection on the client-side.  When the filter is collapsed by the user, the Filterfield control
        /// will set the description of the filter to whatever is returned by this property.  If including script, the
        /// controls parent container can be referenced through a '$content' variable that is set by the control before
        /// referencing this property.
        /// </summary>
        /// <value>
        /// The client format script.
        /// </value>
        public override string GetClientFormatSelection( Type entityType )
        {
            return @"
function() {
  var tagName = $('.js-tag-filter-list', $content).find(':selected').text()
  var result = 'Tagged as ' + tagName;

  return result;
}
";
        }

        /// <summary>
        /// Formats the selection.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <param name="selection">The selection.</param>
        /// <returns></returns>
        public override string FormatSelection( Type entityType, string selection )
        {
            string result = "Group Tag";
            string[] selectionValues = selection.Split( '|' );
            if ( selectionValues.Length >= 2 )
            {
                Guid selectedTagGuid = selectionValues[1].AsGuid();
                var selectedTag = new TagService( new RockContext() ).Get( selectedTagGuid );
                if ( selectedTag != null )
                {
                    result = string.Format( "Tagged as {0}", selectedTag.Name );
                }
            }

            return result;
        }

        /// <summary>
        /// Creates the child controls.
        /// </summary>
        /// <returns></returns>
        public override Control[] CreateChildControls( Type entityType, FilterField filterControl )
        {
            var rblTagType = new RockRadioButtonList();
            rblTagType.ID = filterControl.ID + "_tagType";
            rblTagType.Label = "Tag Type";
            rblTagType.RepeatDirection = RepeatDirection.Horizontal;
            rblTagType.Items.Add( new ListItem( "Personal Tags", "1" ) );
            rblTagType.Items.Add( new ListItem( "Organizational Tags", "2" ) );
            rblTagType.SelectedValue = "1";
            rblTagType.AutoPostBack = true;
            rblTagType.SelectedIndexChanged += rblTagType_SelectedIndexChanged;
            rblTagType.CssClass = "js-tag-type";
            filterControl.Controls.Add( rblTagType );

            var ddlTagList = new RockDropDownList();
            ddlTagList.ID = filterControl.ID + "_ddlTagList";
            ddlTagList.Label = "Tag";
            ddlTagList.AddCssClass( "js-tag-filter-list" );
            filterControl.Controls.Add( ddlTagList );

            PopulateTagList( filterControl );

            return new Control[2] { rblTagType, ddlTagList };
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the rblTagType control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void rblTagType_SelectedIndexChanged( object sender, EventArgs e )
        {
            var filterField = ( sender as Control ).FirstParentControlOfType<FilterField>();
            PopulateTagList( filterField );
        }

        /// <summary>
        /// Populates the tag list.
        /// </summary>
        private void PopulateTagList( FilterField filterField )
        {
            int entityTypeGroupId = EntityTypeCache.GetId( typeof( Rock.Model.Group ) ) ?? 0;
            var tagQry = new TagService( new RockContext() ).Queryable( "OwnerPersonAlias" ).Where( a => a.EntityTypeId == entityTypeGroupId );

            var rblTagType = filterField.ControlsOfTypeRecursive<RockRadioButtonList>().FirstOrDefault( a => a.HasCssClass( "js-tag-type" ) );
            var ddlTagList = filterField.ControlsOfTypeRecursive<RockDropDownList>().FirstOrDefault( a => a.HasCssClass( "js-tag-filter-list" ) );
            RockPage rockPage = rblTagType.Page as RockPage;

            if ( rblTagType.SelectedValueAsInt() == 1 )
            {
                // Personal tags - tags where the ownerid is the current person id
                tagQry = tagQry.Where( a => a.OwnerPersonAlias.PersonId == rockPage.CurrentPersonId ).OrderBy( a => a.Name );
            }
            else
            {
                // Organizational tags - tags where the ownerid is null
                tagQry = tagQry.Where( a => a.OwnerPersonAlias == null ).OrderBy( a => a.Name );
            }

            ddlTagList.Items.Clear();
            var tempTagList = tagQry.ToList();

            foreach ( var tag in tagQry.Select( a => new { a.Guid, a.Name } ) )
            {
                ddlTagList.Items.Add( new ListItem( tag.Name, tag.Guid.ToString() ) );
            }
        }

        /// <summary>
        /// Renders the controls.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <param name="filterControl">The filter control.</param>
        /// <param name="writer">The writer.</param>
        /// <param name="controls">The controls.</param>
        public override void RenderControls( Type entityType, FilterField filterControl, HtmlTextWriter writer, Control[] controls )
        {
            base.RenderControls( entityType, filterControl, writer, controls );
        }

        /// <summary>
        /// Gets the selection.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <param name="controls">The controls.</param>
        /// <returns></returns>
        public override string GetSelection( Type entityType, Control[] controls )
        {
            return ( controls[0] as RadioButtonList ).SelectedValue + "|" + ( controls[1] as RockDropDownList ).SelectedValue;
        }

        /// <summary>
        /// Sets the selection.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <param name="controls">The controls.</param>
        /// <param name="selection">The selection.</param>
        public override void SetSelection( Type entityType, Control[] controls, string selection )
        {
            string[] selectionValues = selection.Split( '|' );

            if ( selectionValues.Length >= 2 )
            {
                int tagType = selectionValues[0].AsInteger();
                Guid selectedTagGuid = selectionValues[1].AsGuid();

                ( controls[0] as RadioButtonList ).SelectedValue = tagType.ToString();

                var rblTagType = controls[0] as RadioButtonList;

                rblTagType_SelectedIndexChanged( rblTagType, new EventArgs() );

                RockDropDownList ddlTagList = controls[1] as RockDropDownList;

                if ( ddlTagList.Items.FindByValue( selectedTagGuid.ToString() ) != null )
                {
                    ddlTagList.SelectedValue = selectedTagGuid.ToString();
                }
                else
                {
                    // if the selectedTag is a personal tag, but for a different Owner than the current logged in person, include it in the list
                    var selectedTag = new TagService( new RockContext() ).Get( selectedTagGuid );
                    if ( selectedTag != null )
                    {
                        if ( selectedTag.OwnerPersonAliasId.HasValue )
                        {
                            foreach ( var listItem in ddlTagList.Items.OfType<ListItem>() )
                            {
                                listItem.Attributes["OptionGroup"] = "Personal";
                            }

                            string tagText = string.Format( "{0} ( {1} )", selectedTag.Name, selectedTag.OwnerPersonAlias.Person );
                            ListItem currentTagListItem = new ListItem( tagText, selectedTagGuid.ToString() );
                            currentTagListItem.Attributes["OptionGroup"] = "Current";
                            ddlTagList.Items.Insert( 0, currentTagListItem );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the expression.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <param name="serviceInstance">The service instance.</param>
        /// <param name="parameterExpression">The parameter expression.</param>
        /// <param name="selection">The selection.</param>
        /// <returns></returns>
        public override Expression GetExpression( Type entityType, IService serviceInstance, ParameterExpression parameterExpression, string selection )
        {
            string[] selectionValues = selection.Split( '|' );
            if ( selectionValues.Length >= 2 )
            {
                Guid tagGuid = selectionValues[1].AsGuid();
                var tagItemQry = new TaggedItemService( ( RockContext )serviceInstance.Context ).Queryable()
                    .Where( x => x.Tag.Guid == tagGuid );

                var qry = new GroupService( ( RockContext )serviceInstance.Context ).Queryable()
                    .Where( g => tagItemQry.Any( x => x.EntityGuid == g.Guid ) );

                return FilterExpressionExtractor.Extract<Rock.Model.Group>( qry, parameterExpression, "g" );
            }

            return null;
        }

        #endregion
    }
}