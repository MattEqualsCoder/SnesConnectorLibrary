-- original file found in a GPLv3 code repository, unclear if this is the intended license nor who the authors are
-- SNI modifications by Berserker, jsd1982; modifications licensed under MIT License
-- version 3 changes Read response from JSON to HEX
-- lua 5.1/5.4 shim by zig; modifications licensed under MIT and WTFPL
-- version 4 enhances the message processing loop to allow for more than one message per game frame
-- Above changes were merged into a previous autotracking lua script by MattEqualsCoder

local emulator = loadfile('emulator.lua')()

function get_os()
    local the_os, ext, arch
    if package.config:sub(1,1) == "\\" then
        the_os, ext = "windows", "dll"
        arch = os.getenv"PROCESSOR_ARCHITECTURE"
    else
        -- TODO: macos?
        the_os, ext = "linux", "so"
        arch = "x86_64" -- TODO: read ELF header from /proc/$PID/exe to get arch
    end

    if arch:find("64") ~= nil then
        arch = "x64"
    else
        arch = "x86"
    end

    return the_os, ext, arch
end

function get_socket_path()
    local the_os, ext, arch = get_os()
    -- for some reason ./ isn't working, so use a horrible hack to get the pwd
    local pwd = (io.popen and io.popen("cd"):read'*l') or "."
	return pwd .. "/" .. arch .. "/socket-" .. the_os .. "-" .. emulator.get_lua_version() .. "." .. ext
end
local socket_path = get_socket_path()
print("loading " .. socket_path)
local socket = assert(package.loadlib(socket_path, "luaopen_socket_core"))()

local json = loadfile('json.lua')()
if emulator.init() ~= true then
    return
end
print("Emulator: " .. emulator.name)

local HOST_ADDRESS = '127.0.0.1'
local HOST_PORT = 6969
local DISCONNECT_DELAY = 10
local RECONNECT_DELAY = 10

local tcp = nil
local connected = false
local lastConnectionAttempt = os.time()
local lastMessage = os.time()
local part = nil

local function ends_with(str, ending)
   return ending == "" or str:sub(-#ending) == ending
end

local function check_for_message()
    local data, err, tempPart = tcp:receive(n, part)
    if data == nil then
        if err ~= 'timeout' then
            emulator.print('Connection lost:' .. err)
            connected = false
        else
            part = tempPart
        end
    else
        part = nil
    end
    
    if part ~= nil and ends_with(part, "\0") then
        data = part
        part = nil
        return data
    else
        return nil
    end

end

local function send_json(data)
    -- print(json.encode(data))
    local ret, err = tcp:send(json.encode(data) .. "\n")
    if ret == nil then
        print('Failed to send:', err)
    end
end

local function process_message(message)
    local data = json.decode(message)
    local action = data['Action']
    local domain = data['Domain']
    local address = data['Address']
    local length = data['Length']
    local values = data['WriteValues']

    local bytes = nil

    if (action == 'read_block') then
        bytes = emulator.read_bytes(address, length, domain)
    elseif (action == 'write_bytes') then
        emulator.write_bytes(address, values, domain)
    elseif (action == 'version') then
        local result = {
            Action = action,
            Value = emulator.name
        }
        send_json(result)
    end

    if (bytes ~= nil) then
        local result = {
            Action = action,
            Address = address,
            Length = length,
            Bytes = bytes
        }
        send_json(result)
    end
end

local function connect()
    tcp = socket.tcp()
    lastConnectionAttempt = os.time()
    print('Attempting to connect')

    local ret, err = tcp:connect(HOST_ADDRESS, HOST_PORT)
    if ret == 1 then
        emulator.print('Connection established')
        tcp:settimeout(0)
        connected = true
        lastMessage = os.time()
    else
        emulator.print('Failed to open socket:' .. err)
        tcp:close()
        tcp = nil
        connected = false
    end
end

local function on_tick()
    if connected then
        local message = check_for_message()
        while message ~= nil do
            -- print(message)
            process_message(message);
            lastMessage = os.time()
            message = check_for_message()
        end

        local currentTime = os.time()
        if lastMessage + DISCONNECT_DELAY <= currentTime then
            connected = false
        end
    else
        local currentTime = os.time()
        if lastConnectionAttempt + RECONNECT_DELAY <= currentTime then
            connect()
        end
    end
end

emulator.start_tick(on_tick)
connect()

