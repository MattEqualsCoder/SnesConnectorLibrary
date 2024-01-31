-- original file found in a GPLv3 code repository, unclear if this is the intended license nor who the authors are
-- SNI modifications by Berserker, jsd1982; modifications licensed under MIT License
-- version 3 changes Read response from JSON to HEX
-- lua 5.1/5.4 shim by zig; modifications licensed under MIT and WTFPL
-- version 4 enhances the message processing loop to allow for more than one message per game frame
-- Above changes were merged into a previous autotracking lua script by MattEqualsCoder

local emulator = { }

local function get_lua_version()
    local major, minor = _VERSION:match("Lua (%d+)%.(%d+)")
    assert(tonumber(major) == 5)
    if tonumber(minor) >= 4 then
        return "5-4"
    end
    return "5-1"
end

function emulator.get_lua_version()
    return get_lua_version()
end

function emulator.init()
    if not event then
        emulator.is_snes9x = true
        emulator.name = "snes9x"
        return true
    else
        emulator.is_snes9x = false
        emulator.name = "BizHawk"
        if emu.getsystemid() ~= "SNES" then
            print("Connector only for BSNES Core within Bizhawk, sorry.")
            return false
        end
        local current_engine = nil;
        if client.get_lua_engine ~= nil then
            current_engine = client.get_lua_engine();
        elseif emu.getluacore ~= nil then
            current_engine = emu.getluacore();
        end
        if current_engine ~= nil and current_engine ~= "LuaInterface" and get_lua_version() ~= "5-4" then
            print("Wrong Lua Core. Found " .. current_engine .. ", was expecting LuaInterface. ")
            print("Please go to Config -> Customize -> Advanced and select Lua+LuaInterface.")
            print("Once set, restart Bizhawk.")
            return false
        end
    end
    return true
end


function emulator.translate_address(address, domain)
    if is_snes9x then
        if domain == "WRAM" then
            return address - 0x7e0000;
        elseif domain == "CARTRAM" then
            local offset = 0x0
            local remaining_addr = address - 0xA06000
            while remaining_addr >= 0x2000 do
                remaining_addr = remaining_addr - 0x10000
                offset = offset + 0x2000
            end
            return offset + remaining_addr
        elseif domain == "CARTROM" then
            return address
        end
    else
        return address
    end
end


function emulator.start_tick(callback)
    if emulator.is_snes9x then
        emu.registerbefore(callback)
    else
        while true do
            callback()
            emu.yield()
        end
    end
end

function emulator.print(message)
    if emulator.is_snes9x then
        emu.message(message)
    else
        gui.addmessage(message)
    end
	print(message)
end

function emulator.read_bytes(address, length, domain)
    local response = memory.readbyterange(emulator.translate_address(address, domain), length, domain)
    if emulator.is_snes9x then
        return response
    else
        local cleaned_response = {}

        for i = 0, length - 1 do
            table.insert(cleaned_response, tonumber(response[i]))
        end

        return cleaned_response
    end
end

function emulator.write_byte(address, value, domain)
    memory.writebyte(emulator.translate_address(address, domain), value, domain)
end

function emulator.write_bytes(address, values, domain)
    local adr = tonumber(address)
    for k, v in pairs(values) do
        emulator.write_byte(adr + k - 1, tonumber(v), domain)
    end
end

function emulator.write_uint16(address, value, domain)
    memory.writeword(emulator.translate_address(address, domain), value, domain)
end

function emulator.get_name()
    return emulator.name
end

return emulator;
