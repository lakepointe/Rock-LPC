﻿<%@ Control Language="C#" AutoEventWireup="true" CodeFile="GroupTypeChanger.ascx.cs" Inherits="RockWeb.Plugins.org_secc.Administration.GroupTypeChanger" %>

<!-- LPC MODIFIED CODE - Made major changes to this block to support v14 of Rock, to improve performance, to delete orphaned attribute values, and to improve the ease of usability of this block -->

<asp:UpdatePanel ID="upReport" runat="server">
    <ContentTemplate>
        <Rock:NotificationBox runat="server" ID="nbSuccess" Visible="false" NotificationBoxType="Success" Heading="<span style='font-size: 20px'>GroupType successfully changed.</span>"></Rock:NotificationBox>
        <div class="row">
            <div class="col-sm-6">
                <Rock:RockLiteral ID="ltName" runat="server" Label="Group Name"></Rock:RockLiteral>
            </div>
            <div class="col-sm-6">
                <Rock:RockLiteral ID="ltGroupTypeName" runat="server" Label="Group Type"></Rock:RockLiteral>
            </div>
        </div>
        <Rock:RockDropDownList runat="server" ID="ddlGroupTypes" Label="New Group Type" DataValueField="Id" DataTextField="Name"
            AutoPostBack="true" OnSelectedIndexChanged="ddlGroupTypes_SelectedIndexChanged" EnhanceForLongLists="true">
        </Rock:RockDropDownList>
        <div class="col-sm-6">
            <asp:Panel runat="server" ID="pnlRoles" Visible="false">
                <h3>Group Member Role Mappings</h3>
                <asp:PlaceHolder runat="server" ID="phRoles" />
            </asp:Panel>
        </div>
        <div class="col-sm-6">
            <asp:Panel runat="server" ID="pnlMemberAttributes" Visible="false">
                <h3>Group Member Group Type Attribute Mappings</h3>
                <asp:PlaceHolder runat="server" ID="phMemberAttributes" />
            </asp:Panel>
        </div>
        <div class="col-sm-6">
            <asp:Panel runat="server" ID="pnlGroupAttributes" Visible="false">
                <h3>Group Attribute Mappings</h3>
                <asp:PlaceHolder runat="server" ID="phGroupAttributes" />
            </asp:Panel>
        </div>
        <div class="col-xs-12">
            <Rock:BootstrapButton runat="server" ID="btnSave" CssClass="btn btn-primary" Text="Save"
                 Visible="false" OnClick="btnSave_Click"></Rock:BootstrapButton>
        </div>

    </ContentTemplate>
</asp:UpdatePanel>
