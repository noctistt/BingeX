// SPDX-License-Identifier: GPL-3.0-or-later
package com.aayush262.dartotsu_extension_bridge.logger
// Original quiet adapter: provider/runtime diagnostics must never enter protocol stdout.
object Logger {
    @JvmStatic fun log(message:String,level:LogLevel=LogLevel.INFO):Int = 1
}
enum class LogLevel { ERROR, WARNING, INFO, DEBUG }
