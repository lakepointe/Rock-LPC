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

import { defineComponent, PropType } from "vue";
import RockButton from "@Obsidian/Controls/rockButton";
import { ListItemBag } from "@Obsidian/ViewModels/Utility/listItemBag";

export default defineComponent({
    name: "Cms.ContentCollectionDetail.SearchFilter",

    components: {
        RockButton
    },

    props: {
        /** True if the filter is enabled for use. */
        isEnabled: {
            type: Boolean as PropType<boolean>,
            default: false
        },
        
        /** True if the sources are in an inconsistent state which prevents editing. */
        isInconsistent: {
            type: Boolean as PropType<boolean>,
            default: false
        },

        /** The title of the filter (attribute name). */
        title: {
            type: String as PropType<string>,
            required: true
        },

        /** The subtitle of the filter (field type). */
        subtitle: {
            type: String as PropType<string>,
            required: false
        },

        /** The description of the filter. */
        description: {
            type: String as PropType<string>,
            required: false
        },

        /** The value pairs to display showing the details of this filter. */
        values: {
            type: Array as PropType<ListItemBag[]>,
            required: false
        },
    },

    emits: {
        "edit": () => true
    },

    setup(props, { emit }) {
        const onEditClick = (): void => {
            emit("edit");
        };

        return {
            onEditClick
        };
    },

    template: `
<div class="search-filter-row">
    <div class="search-filter-icon">
        <i v-if="isEnabled" class="fa fa-check-square" style="color: var(--brand-color);"></i>
        <i v-else class="fa fa-check-square-o" style="color: #c3c2c2;"></i>
    </div>

    <div class="search-filter-content">
        <div class="search-filter-title">
            <span class="title">{{ title }}</span>
            <template v-if="subtitle">&nbsp;<span class="subtitle text-sm text-muted">{{ subtitle }}</span></template>
        </div>
        <div v-if="description" class="search-filter-description">{{ description }}</div>

        <fieldset v-if="!isInconsistent">
            <dl v-for="value in values">
                <dt>{{ value.text }}</dt>
                <dd>{{ value.value }}</dd>
            </dl>
        </fieldset>
        <div v-else class="text-danger margin-t-md margin-b-md">
            The field type configuration of the attribute is not consistent for all sources. Please resolve the inconsistency or rename the attribute key to be unique.
        </div>
    </div>

    <div class="search-filter-actions">
        <RockButton v-if="!isInconsistent" btnSize="sm" @click="onEditClick"><i class="fa fa-pencil"></i></RockButton>
    </div>
</div>
`
});
