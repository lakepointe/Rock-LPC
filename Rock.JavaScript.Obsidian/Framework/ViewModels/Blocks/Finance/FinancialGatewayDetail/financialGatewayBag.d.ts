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

import { ListItemBag } from "@Obsidian/ViewModels/Utility/listItemBag";
import { PublicAttributeBag } from "@Obsidian/ViewModels/Utility/publicAttributeBag";

/** Used to store options for the FinancialGateway */
export type FinancialGatewayBag = {
    /** Gets or sets the attributes. */
    attributes?: Record<string, PublicAttributeBag> | null;

    /** Gets or sets the attribute values. */
    attributeValues?: Record<string, string> | null;

    /** Gets or sets the batch schedule whether Weekly or Daily. */
    batchSchedule?: string | null;

    /** Gets or sets the batch start day if BtachSchedule is set to Weekly. */
    batchStartDay?: string | null;

    /**
     * Gets the batch time offset (in ticks). By default online payments will be grouped into batches with a start time
     * of 12:00:00 AM.  However if the payment gateway groups transactions into batches based on a different
     * time, this offset can specified so that Rock will use the same time when creating batches for online
     * transactions
     */
    batchTimeOffsetTicks?: string | null;

    /** Gets or sets the user defined description of the FinancialGateway. */
    description?: string | null;

    /** Gets or sets the type of the gateway entity. */
    entityType?: ListItemBag | null;

    /** Gets or sets the identifier key of this entity. */
    idKey?: string | null;

    /** Gets or sets a value indicating whether this instance is active. */
    isActive: boolean;

    /** Gets or sets the (internal) Name of the FinancialGateway. This property is required. */
    name?: string | null;
};
