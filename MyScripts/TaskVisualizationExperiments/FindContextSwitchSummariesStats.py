spawns = 0
continuations = 0
noNewOp = 0
contextSwitches = 0
while True:
    try:
        line = input()
        line_array = list(line.split())
        if line_array != []:
            if line_array[0] == "SUMMARY:":
                contextSwitches += 1
                # print(line)
                # print(line_array)
                if line_array[1] == "No":
                    noNewOp += 1
                elif line_array[1] == "Spawn":
                    spawns += 1
                elif line_array[1] == "Continuation":
                    continuations
                else:
                    print("ERROR!")

                
    except:
        break

print("contextSwitches: " + str(contextSwitches) + ", noNewOp: " + str(noNewOp) + ", spawns: " + str(spawns) + ", continuations:" + str(continuations))
noNewOp_percent = (noNewOp / contextSwitches) * 100 
spawns_percent = (spawns / contextSwitches) * 100 
continuations_percent = (continuations / contextSwitches) * 100 
print("noNewOp %: " + str(noNewOp_percent) + ", spawns %: " + str(spawns_percent) + ", continuations %: " + str(continuations_percent))
