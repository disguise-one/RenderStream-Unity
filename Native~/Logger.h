#pragma once
#include <memory>
#include <string>

#include "Unity/IUnityLog.h"

namespace NativeRenderingPlugin
{
    class Logger
    {
    public:

        Logger(IUnityInterfaces* unityInterfaces) :
            m_Log(nullptr),
            m_IsInitialized(false)
        {
            m_Log = unityInterfaces->Get<IUnityLog>();

            if (m_Log != nullptr)
            {
                m_IsInitialized = true;
            }
        }

        bool IsInitialized() const
        {
            return m_IsInitialized;
        }

        void LogWarning(const char* msg)
        {
            if (m_IsInitialized)
                UNITY_LOG_WARNING(m_Log, msg);
        }

        void LogWarning(const char* msg, int errorCode)
        {
            if (m_IsInitialized)
                UNITY_LOG_WARNING(m_Log, FormatErrorMessage(msg, errorCode).c_str());
        }

        void LogError(const char* msg)
        {
            if (m_IsInitialized)
                UNITY_LOG_ERROR(m_Log, msg);
        }

        void LogError(const char* msg, int errorCode)
        {
            if (m_IsInitialized)
                UNITY_LOG_ERROR(m_Log, FormatErrorMessage(msg, errorCode).c_str());
        }

    private:

        static std::string FormatErrorMessage(const char* msg, int errorCode)
        {
            std::string str = msg;
            str += std::to_string(errorCode);
            return str;
        }

        IUnityLog* m_Log;
        bool m_IsInitialized;
    };

    inline std::unique_ptr<Logger> s_Logger;
}
