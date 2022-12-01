#pragma once
#include <string>

std::wstring StrToWstr(const std::string& str)
{
    auto length = str.length();
    std::wstring wstr(length, 0);
    for (size_t i = 0; i < length; ++i)
    {
        wstr[i] = static_cast<std::wstring::value_type>(str[i]);
    }
    return wstr;
}

std::wstring StrToWstr(const char* str)
{
    return StrToWstr(std::string(str));
}
