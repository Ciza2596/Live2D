﻿/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

namespace Live2D.Cubism.Framework
{
    /// <summary>
    /// Cubism update interface.
    /// </summary>
    public interface ILateUpdatable
    {
        int ExecutionOrder { get; }
        bool NeedsUpdateOnEditing { get; }
        bool HasUpdateController { get; set; }
        void OnLateUpdate();
    }
}
