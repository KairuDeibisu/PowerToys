﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FileActionsMenu.Ui.Actions
{
    internal interface IActionAndRequestCheckedMenuItems : IAction
    {
        public CheckedMenuItemsDictionary CheckedMenuItemsDictionary { get; set; }
    }
}
