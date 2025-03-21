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
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using Rock;
using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Web;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;
using Rock.Security;

namespace RockWeb.Plugins.com_kfs.Core
{
    #region Block Attributes

    [DisplayName( "KFS Category Detail" )]
    [Category( "KFS > Core" )]
    [Description( "Displays the details of a given category." )]

    [EntityTypeField( "Entity Type", "The type of entity to associate category with" )]
    [TextField( "Entity Type Qualifier Property", "", false )]
    [TextField( "Entity Type Qualifier Value", "", false )]

    [CategoryField( "Root Category", "Select the root category to use as a starting point for the parent category picker.", false, required:false, category: "CustomSetting" )]
    [CategoryField( "Exclude Categories", "Select any category that you need to exclude from the parent category picker", true, required:false, category: "CustomSetting" )]

    [GroupTypeField( "Associated Group Type", "Select a Group Type to trigger the creation of a new group of selected type upon creating a new Category. The new Category's 'AssociatedGroup' attribute will be set to the new Group.", false, "", "", 0, "GroupTypeSetting", "" )]

    #endregion

    public partial class KFSCategoryDetail : RockBlockCustomSettings
    {
        #region Control Methods

        private int entityTypeId = 0;
        private string entityTypeQualifierProperty = string.Empty;
        private string entityTypeQualifierValue = string.Empty;

        /// <summary>
        /// Gets the settings tool tip.
        /// </summary>
        /// <value>
        /// The settings tool tip.
        /// </value>
        public override string SettingsToolTip
        {
            get
            {
                return "Set Category Options";
            }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            var entityTypeGuid = Guid.Empty;
            if ( Guid.TryParse( GetAttributeValue( "EntityType" ), out entityTypeGuid ) )
            {
                entityTypeId = EntityTypeCache.Get( entityTypeGuid ).Id;
            }
            entityTypeQualifierProperty = GetAttributeValue( "EntityTypeQualifierProperty" );
            entityTypeQualifierValue = GetAttributeValue( "EntityTypeQualifierValue" );

            btnSecurity.EntityTypeId = EntityTypeCache.Get( typeof( Rock.Model.Category ) ).Id;

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upDetail );

            mdCategoryDetailConfig.Visible = false;
        }

        /// <summary>
        /// Handles the BlockUpdated event of the Block control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            NavigateToPage( this.RockPage.Guid, new Dictionary<string, string>() );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                var categoryIdParam = PageParameter( "CategoryId" );
                if ( !string.IsNullOrEmpty( categoryIdParam ) )
                {
                    ShowDetail( categoryIdParam.AsInteger(), PageParameter( "ParentCategoryId" ).AsIntegerOrNull() );
                }
                else
                {
                    pnlDetails.Visible = false;
                }
            }
        }

        #endregion

        #region Edit Events

        /// <summary>
        /// Handles the Click event of the btnCancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnCancel_Click( object sender, EventArgs e )
        {
            if ( hfCategoryId.Value.Equals( "0" ) )
            {
                var parentCategoryId = PageParameter( "ParentCategoryId" ).AsIntegerOrNull();
                if ( parentCategoryId.HasValue )
                {
                    // Cancelling on Add, and we know the parentCategoryId, so we are probably in treeview mode, so navigate to the current page
                    var qryParams = new Dictionary<string, string>();
                    qryParams["CategoryId"] = parentCategoryId.ToString();
                    NavigateToPage( RockPage.Guid, qryParams );
                }
                else
                {
                    // Cancelling on Add.  Return to Grid
                    NavigateToParentPage();
                }
            }
            else
            {
                // Cancelling on Edit.  Return to Details
                var service = new CategoryService( new RockContext() );
                var category = service.Get( hfCategoryId.ValueAsInt() );
                ShowReadonlyDetails( category );
            }
        }

        /// <summary>
        /// Handles the Click event of the btnEdit control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnEdit_Click( object sender, EventArgs e )
        {
            var service = new CategoryService( new RockContext() );
            var category = service.Get( hfCategoryId.ValueAsInt() );
            ShowEditDetails( category );
        }

        /// <summary>
        /// Handles the Click event of the btnDelete control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnDelete_Click( object sender, EventArgs e )
        {
            int? parentCategoryId = null;

            var rockContext = new RockContext();
            var categoryService = new CategoryService( rockContext );
            var category = categoryService.Get( int.Parse( hfCategoryId.Value ) );

            if ( category != null )
            {
                string errorMessage;
                if ( !categoryService.CanDelete( category, out errorMessage ) )
                {
                    ShowReadonlyDetails( category );
                    mdDeleteWarning.Show( errorMessage, ModalAlertType.Information );
                }
                else
                {
                    parentCategoryId = category.ParentCategoryId;

                    CategoryCache.Remove( category.Id );

                    categoryService.Delete( category );
                    rockContext.SaveChanges();

                    // reload page, selecting the deleted category's parent
                    var qryParams = new Dictionary<string, string>();
                    if ( parentCategoryId != null )
                    {
                        qryParams["CategoryId"] = parentCategoryId.ToString();
                    }

                    NavigateToPage( RockPage.Guid, qryParams );
                }
            }
        }

        /// <summary>
        /// Sets the edit mode.
        /// </summary>
        /// <param name="editable">if set to <c>true</c> [editable].</param>
        private void SetEditMode( bool editable )
        {
            pnlEditDetails.Visible = editable;
            fieldsetViewDetails.Visible = !editable;
        }

        /// <summary>
        /// Handles the Click event of the btnSave control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnSave_Click( object sender, EventArgs e )
        {
            Category category;

            var rockContext = new RockContext();
            var categoryService = new CategoryService( rockContext );

            var categoryId = hfCategoryId.ValueAsInt();

            if ( categoryId == 0 )
            {
                category = new Category
                {
                    IsSystem = false,
                    EntityTypeId = entityTypeId,
                    EntityTypeQualifierColumn = entityTypeQualifierProperty,
                    EntityTypeQualifierValue = entityTypeQualifierValue,
                    Order = 0
                };
                categoryService.Add( category );
            }
            else
            {
                category = categoryService.Get( categoryId );
            }

            category.Name = tbName.Text;
            category.ParentCategoryId = cpParentCategory.SelectedValueAsInt();
            category.IconCssClass = tbIconCssClass.Text;
            category.HighlightColor = tbHighlightColor.Text;

            var orphanedBinaryFileIdList = new List<int>();

            if ( !Page.IsValid )
            {
                return;
            }

            // if the category IsValid is false, and the UI controls didn't report any errors, it is probably because the custom rules of category didn't pass.
            // So, make sure a message is displayed in the validation summary
            cvCategory.IsValid = category.IsValid;

            if ( !cvCategory.IsValid )
            {
                cvCategory.ErrorMessage = category.ValidationResults.Select( a => a.ErrorMessage ).ToList().AsDelimited( "<br />" );
                return;
            }

            var binaryFileService = new BinaryFileService( rockContext );
            foreach ( int binaryFileId in orphanedBinaryFileIdList )
            {
                var binaryFile = binaryFileService.Get( binaryFileId );
                if ( binaryFile != null )
                {
                    // marked the old images as IsTemporary so they will get cleaned up later
                    binaryFile.IsTemporary = true;
                }
            }

            rockContext.SaveChanges();
            CategoryCache.Remove( category.Id );

            category = new CategoryService( new RockContext() ).Get( category.Id );

            Guid groupTypeGuid;
            if ( Guid.TryParse( this.GetAttributeValue( "GroupTypeSetting" ), out groupTypeGuid ) )
            {
                var attributeKey = "AssociatedGroup";
                VerifyCategoryAttribute( rockContext, attributeKey );
                category.LoadAttributes();
                if( category.GetAttributeValue( attributeKey ) == null )
                {
                    Group parentGroup = null;
                    var groupService = new GroupService( rockContext );
                    var groupTypeService = new GroupTypeService( rockContext );
                    var parentCategory = category.ParentCategory;
                    if ( parentCategory != null )
                    {
                        parentCategory.LoadAttributes();
                        if ( parentCategory.GetAttributeValue( attributeKey ) != null )
                        {
                            parentGroup = groupService.Get( Guid.Parse( category.ParentCategory.GetAttributeValue( attributeKey ) ) );
                        }
                    }
                    var newGroup = new Group
                    {
                        Name = category.Name
                    };
                    if ( parentGroup != null )
                    {
                        newGroup.ParentGroup = parentGroup;
                    }
                    newGroup.GroupType = groupTypeService.Get( groupTypeGuid );
                    groupService.Add( newGroup );
                    rockContext.SaveChanges();

                    newGroup = groupService.Get( newGroup.Guid );
                    category.AttributeValues[attributeKey].Value = newGroup.Guid.ToString();
                    category.SaveAttributeValues();
                }
            }

            var qryParams = new Dictionary<string, string>();
            qryParams["CategoryId"] = category.Id.ToString();
            NavigateToPage( RockPage.Guid, qryParams );
        }

        private static void VerifyCategoryAttribute( RockContext rockContext, string attributeKey )
        {
            int? categoryEntityTypeId = null;
            var attributeService = new AttributeService( rockContext );
            Rock.Model.Attribute attribute = null;
            categoryEntityTypeId = EntityTypeCache.Get( typeof( Category ) ).Id;
            var attributeExists = attributeService.Get( categoryEntityTypeId, string.Empty, string.Empty ).Any( a => a.Key == attributeKey );
            if ( !attributeExists )
            {
                var edtAttribute = new Rock.Model.Attribute
                {
                    FieldTypeId = FieldTypeCache.Get( Rock.SystemGuid.FieldType.GROUP_TYPE ).Id,
                    Name = "Associated Group",
                    Key = attributeKey
                };
                attribute = Helper.SaveAttributeEdits( edtAttribute, categoryEntityTypeId, string.Empty, string.Empty );

                EntityTypeAttributesCache.Clear();
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Shows the detail.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        public void ShowDetail( int categoryId )
        {
            ShowDetail( categoryId, null );
        }

        /// <summary>
        /// Shows the detail.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <param name="parentCategoryId">The parent category id.</param>
        public void ShowDetail( int categoryId, int? parentCategoryId )
        {
            pnlDetails.Visible = false;

            var categoryService = new CategoryService( new RockContext() );
            Category category = null;

            if ( !categoryId.Equals( 0 ) )
            {
                category = categoryService.Get( categoryId );
                pdAuditDetails.SetEntity( category, ResolveRockUrl( "~" ) );
            }

            if ( category == null )
            {
                category = new Category
                {
                    Id = 0,
                    IsSystem = false,
                    ParentCategoryId = parentCategoryId,
                    // fetch the ParentCategory (if there is one) so that security can check it
                    ParentCategory = categoryService.Get( parentCategoryId ?? 0 ),
                    EntityTypeId = entityTypeId,
                    EntityTypeQualifierColumn = entityTypeQualifierProperty,
                    EntityTypeQualifierValue = entityTypeQualifierValue
                };
                // hide the panel drawer that show created and last modified dates
                pdAuditDetails.Visible = false;
            }

            if (category.EntityTypeId != entityTypeId || !category.IsAuthorized( Authorization.VIEW, CurrentPerson ) )
            {
                pnlDetails.Visible = false;
                return;
            }

            pnlDetails.Visible = true;
            hfCategoryId.Value = category.Id.ToString();

            // render UI based on Authorized and IsSystem
            var readOnly = false;

            nbEditModeMessage.Text = string.Empty;
            
            // if the person is Authorized to EDIT the category, or has EDIT for the block
            var canEdit = category.IsAuthorized( Authorization.EDIT, CurrentPerson ) || this.IsUserAuthorized(Authorization.EDIT);
            if ( !canEdit )
            {
                readOnly = true;
                nbEditModeMessage.Text = EditModeMessage.ReadOnlyEditActionNotAllowed( Category.FriendlyTypeName );
            }

            if ( category.IsSystem )
            {
                readOnly = true;
                nbEditModeMessage.Text = EditModeMessage.ReadOnlySystem( Category.FriendlyTypeName );
            }

            btnSecurity.Visible = category.IsAuthorized( Authorization.ADMINISTRATE, CurrentPerson );
            btnSecurity.Title = category.Name;
            btnSecurity.EntityId = category.Id;

            if ( readOnly )
            {
                btnEdit.Visible = false;
                btnDelete.Visible = false;
                ShowReadonlyDetails( category );
            }
            else
            {
                btnEdit.Visible = true;
                var errorMessage = string.Empty;
                btnDelete.Visible = true;
                btnDelete.Enabled = categoryService.CanDelete(category, out errorMessage);
                btnDelete.ToolTip = btnDelete.Enabled ? string.Empty : errorMessage;

                if ( category.Id > 0 )
                {
                    ShowReadonlyDetails( category );
                }
                else
                {
                    ShowEditDetails( category );
                }
            }

            if ( btnDelete.Visible && btnDelete.Enabled )
            {
                btnDelete.Attributes["onclick"] = string.Format( "javascript: return Rock.dialogs.confirmDelete(event, '{0}');", Category.FriendlyTypeName.ToLower() );
            }
        }

        /// <summary>
        /// Shows the edit details.
        /// </summary>
        /// <param name="category">The category.</param>
        private void ShowEditDetails( Category category )
        {
            if ( category.Id > 0 )
            {
                lTitle.Text = ActionTitle.Edit( Category.FriendlyTypeName ).FormatAsHtmlTitle();
                lIcon.Text = "<i class='fa fa-square-o'></i>";
            }
            else
            {
                lTitle.Text = ActionTitle.Add( Category.FriendlyTypeName ).FormatAsHtmlTitle();

                if ( !string.IsNullOrEmpty( category.IconCssClass ) )
                {
                    lIcon.Text = String.Format( "<i class='{0}'></i>", category.IconCssClass );
                }
                else
                {
                    lIcon.Text = "<i class='fa fa-square-o'></i>";
                }
            }

            SetEditMode( true );

            tbName.Text = category.Name;

            if ( category.EntityTypeId != 0 )
            {
                var entityType = EntityTypeCache.Get( category.EntityTypeId );
                lblEntityTypeName.Text = entityType.Name;
            }
            else
            {
                lblEntityTypeName.Text = string.Empty;
            }

            var excludeCategoriesGuids = this.GetAttributeValue( "ExcludeCategories" ).SplitDelimitedValues().AsGuidList();
            var excludedCategoriesIds = new List<int>();
            if ( excludeCategoriesGuids != null && excludeCategoriesGuids.Any() )
            {
                foreach ( var excludeCategoryGuid in excludeCategoriesGuids )
                {
                    var excludedCategory = CategoryCache.Get( excludeCategoryGuid );
                    if ( excludedCategory != null )
                    {
                        excludedCategoriesIds.Add( excludedCategory.Id );
                    }
                }
            }

            cpParentCategory.EntityTypeId = category.EntityTypeId;
            cpParentCategory.EntityTypeQualifierColumn = category.EntityTypeQualifierColumn;
            cpParentCategory.EntityTypeQualifierValue = category.EntityTypeQualifierValue;
            cpParentCategory.ExcludedCategoryIds = excludedCategoriesIds.AsDelimited( "," );
            var rootCategory = CategoryCache.Get( this.GetAttributeValue( "RootCategory" ).AsGuid() );

            cpParentCategory.RootCategoryId = rootCategory != null ? rootCategory.Id : (int?)null;
            cpParentCategory.SetValue( category.ParentCategoryId );

            lblEntityTypeQualifierColumn.Visible = !string.IsNullOrWhiteSpace( category.EntityTypeQualifierColumn );
            lblEntityTypeQualifierColumn.Text = category.EntityTypeQualifierColumn;
            lblEntityTypeQualifierValue.Visible = !string.IsNullOrWhiteSpace( category.EntityTypeQualifierValue );
            lblEntityTypeQualifierValue.Text = category.EntityTypeQualifierValue;
            tbIconCssClass.Text = category.IconCssClass;
            tbHighlightColor.Text = category.HighlightColor;
        }

        /// <summary>
        /// Shows the readonly details.
        /// </summary>
        /// <param name="category">The category.</param>
        private void ShowReadonlyDetails( Category category )
        {
            SetEditMode( false );

            string categoryIconHtml = !string.IsNullOrWhiteSpace( category.IconCssClass ) ?
                categoryIconHtml = string.Format( "<i class='{0} fa-2x' ></i>", category.IconCssClass ) : "";

            hfCategoryId.SetValue( category.Id );
            lTitle.Text = category.Name.FormatAsHtmlTitle();
            if ( !string.IsNullOrEmpty( category.IconCssClass ) )
            {
                lIcon.Text = String.Format( "<i class='{0}'></i>", category.IconCssClass );
            }
            else
            {
                lIcon.Text = "<i class='fa fa-square-o'></i>";
            }

            lblMainDetails.Text = new DescriptionList()
                .Add( "Entity Type", category.EntityType.Name )
                .Html;

        }

        /// <summary>
        /// Shows the settings.
        /// </summary>
        protected override void ShowSettings()
        {
            var rockContext = new RockContext();
            var categoryService = new CategoryService( rockContext );
            var entityType = EntityTypeCache.Get( this.GetAttributeValue( "EntityType" ).AsGuid() );
            var rootCategory = categoryService.Get( this.GetAttributeValue( "RootCategory" ).AsGuid() );

            cpRootCategoryDetail.EntityTypeId = entityType != null ? entityType.Id : 0;

            // make sure the rootCategory matches the EntityTypeId (just in case they changed the EntityType after setting RootCategory
            if ( rootCategory != null && cpRootCategoryDetail.EntityTypeId == rootCategory.EntityTypeId )
            {
                cpRootCategoryDetail.SetValue( rootCategory );
            }
            else
            {
                cpRootCategoryDetail.SetValue( null );
            }

            cpRootCategoryDetail.Enabled = entityType != null;
            nbRootCategoryEntityTypeWarning.Visible = entityType == null;

            var excludedCategories = categoryService.GetByGuids( this.GetAttributeValue( "ExcludeCategories" ).SplitDelimitedValues().AsGuidList() );
            cpExcludeCategoriesDetail.EntityTypeId = entityType != null ? entityType.Id : 0;

            // make sure the excluded categories matches the EntityTypeId (just in case they changed the EntityType after setting excluded categories
            if ( excludedCategories != null && excludedCategories.All( a => a.EntityTypeId == cpExcludeCategoriesDetail.EntityTypeId ) )
            {
                cpExcludeCategoriesDetail.SetValues( excludedCategories );
            }
            else
            {
                cpExcludeCategoriesDetail.SetValue( null );
            }

            mdCategoryDetailConfig.Visible = true;
            mdCategoryDetailConfig.Show();
        }

        /// <summary>
        /// Handles the SaveClick event of the mdCategoryDetailConfig control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void mdCategoryDetailConfig_SaveClick( object sender, EventArgs e )
        {
            var selectedCategory = CategoryCache.Get( cpRootCategoryDetail.SelectedValue.AsInteger() );
            this.SetAttributeValue( "RootCategory", selectedCategory != null ? selectedCategory.Guid.ToString() : string.Empty );

            var excludedCategoryIds = cpExcludeCategoriesDetail.SelectedValuesAsInt();
            var excludedCategoryGuids = new List<Guid>();
            foreach ( int excludedCategoryId in excludedCategoryIds )
            {
                var excludedCategory = CategoryCache.Get( excludedCategoryId );
                if ( excludedCategory != null )
                {
                    excludedCategoryGuids.Add( excludedCategory.Guid );
                }
            }

            this.SetAttributeValue( "ExcludeCategories", excludedCategoryGuids.AsDelimited( "," ) );

            this.SaveAttributeValues();

            mdCategoryDetailConfig.Visible = false;
            mdCategoryDetailConfig.Hide();
            Block_BlockUpdated( sender, e );
        }

        #endregion
    }
}