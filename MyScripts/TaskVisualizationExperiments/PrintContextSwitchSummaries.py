# while True:
#     line = input()
#     line_array = list(line.split())
#     print(line)
#     print(line_array)
#     if line_array[0] == "<TaskSummaryLog>":
#         print(line)
#         print(line_array)

count = 0
while True:
    try:
        line = input()
        line_array = list(line.split())
        # print(line)
        # print(line_array)
        if line_array != []:
            if line_array[0] == "<TaskSummaryLog>":
                count += 1
                if count == 1:
                    # print("skipping first print!")
                    print("Scheduled: Task(0)")
                    print()
                    continue
                # print(line)
                # print(line_array)
                if line_array[1] == "T-case":
                    print("     SUMMARY: ", end = "")
                    if line_array[2] == "1.):":
                        print("No new task added.")
                        print()
                        continue
                    # print("yeah!")
                    parent_task = line_array[11]
                    parent_task_id = int(line_array[14][:-2])
                    new_task = line_array[5]
                    new_task_id = int(line_array[8][:-1])
                    is_continuation = False
                    task_type = "continuation"
                    if line_array[3] == "Spawn":
                        is_continuation = False
                        task_type = "Spawn"
                    elif line_array[3] == "Continuation":
                        is_continuation = True
                        task_type = "Continuation"
                    else:
                        print("error1")
                    # print("parent_task: ", end="")
                    # print(parent_task)
                    # print("parent_task_id: ", end = "")
                    # print(parent_task_id)
                    # print("new_task: ", end="")
                    # print(new_task)
                    # print("new_task_id: ", end = "")
                    # print(new_task_id)
                    # print("is_continuation: ", end="")
                    # print(is_continuation)
                    
                    print(task_type + " " + new_task + " (ID = " + str(new_task_id) + ")" + " created by " + parent_task + " (ID = " + str(parent_task_id) + ")")
                elif line_array[1] == "Scheduled:":
                    print("CONTEXT_SWITCH: ", end = "")
                    # print("meah!")
                    task_scheduled = line_array[2]
                    print("Scheduled: ", end="")
                    print(task_scheduled)
                else:
                    print("error2")
                print()
                
    except:
        break