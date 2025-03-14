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
//

import { CommunicationPreference } from "@Obsidian/Enums/Blocks/Crm/FamilyPreRegistration/communicationPreference";
import { Gender } from "@Obsidian/Enums/Crm/gender";
import { Guid } from "@Obsidian/Types";
import { BirthdayPickerBag } from "@Obsidian/ViewModels/Controls/birthdayPickerBag";
import { PublicAttributeBag } from "@Obsidian/ViewModels/Utility/publicAttributeBag";

/** The bag that contains all the person request information for the Family Pre-Registration block. */
export type FamilyPreRegistrationPersonBag = {
    /** Gets or sets the attributes. */
    attributes?: Record<string, PublicAttributeBag> | null;

    /** Gets or sets the attribute values. */
    attributeValues?: Record<string, string> | null;

    /** Gets or sets the birth date. */
    birthDate?: BirthdayPickerBag | null;

    /** Gets or sets the communication preference. */
    communicationPreference: CommunicationPreference;

    /** Gets or sets the email. */
    email?: string | null;

    /** Gets or sets the ethnicity defined value unique identifier. */
    ethnicityDefinedValueGuid?: Guid | null;

    /** Gets or sets the family role unique identifier. */
    familyRoleGuid?: Guid | null;

    /** Gets or sets the first name. */
    firstName?: string | null;

    /** Gets or sets the gender. */
    gender: Gender;

    /** Gets or sets the grade defined value unique identifier. */
    gradeDefinedValueGuid?: Guid | null;

    /** Gets or sets the person unique identifier. */
    guid: Guid;

    /** Gets or sets a value indicating whether this instance is first name read only. */
    isFirstNameReadOnly: boolean;

    /** Gets or sets a value indicating whether this instance is last name read only. */
    isLastNameReadOnly: boolean;

    /** Gets or sets a value to set PhoneNumber.IsMessagingEnabled for the specified mobie number */
    isMessagingEnabled: boolean;

    /** Gets or sets the last name. */
    lastName?: string | null;

    /** Gets or sets the marital status defined value unique identifier. */
    maritalStatusDefinedValueGuid?: Guid | null;

    /** Gets or sets the mobile phone. */
    mobilePhone?: string | null;

    /** Gets or sets the mobile phone country code. */
    mobilePhoneCountryCode?: string | null;

    /** Gets or sets the profile photo unique identifier. */
    profilePhotoGuid?: Guid | null;

    /** Gets or sets the race defined value unique identifier. */
    raceDefinedValueGuid?: Guid | null;

    /** Gets or sets the suffix defined value unique identifier. */
    suffixDefinedValueGuid?: Guid | null;

    //LPC CODE
    /** Gets or sets the allergy attribute value. */
    allergy?: string | null;

    /** Gets or sets the Self Release attribute value. */
    isSelfRelease: boolean;
    // END LPC CODE
};
