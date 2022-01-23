parent_task_dic = {}
while True:
    try:
        line = input()
        line_array = list(line.split())
        print(line)
        print(line_array)
        if line_array != []:
            if line_array[0] == "SUMMARY:":
                if line_array[1] == "Continuation" or line_array[1] == "Spawn":
                    parent_task = line_array[8]
                    parent_task_dic[parent_task] = 0
                    print("parent_task: " + parent_task)
                    print("parent_task_dic: ", end="")
                    print(parent_task_dic)
            elif line_array[0] == "CONTEXT_SWITCH:":
                scheduled_task = line_array[2]
                print("scheduled_task: " + scheduled_task)
                try:
                    parent_task_dic[scheduled_task] += 1
                    print("UNEXPECTED: parent got scheduled after last creation of a spawn and/or continuation task.")
                except:
                     print("NORMAL: scheduled task is not a previous parent task of an await point")
    except:
        break

print()
print()
print()
print()
print()
print(parent_task_dic)
# TODO: wakh through the parent_task_dic to print whether such case exist?
# TODO: write code to test whether the resumed parent and new task could run concurrently.









# COPY OF STABLE PROOF THAT PARENT DOES RESUME AFTER CONTINUATION
# parent_task_dic = {}
# while True:
#     try:
#         line = input()
#         line_array = list(line.split())
#         print(line)
#         print(line_array)
#         if line_array != []:
#             if line_array[0] == "SUMMARY:":
#                 if line_array[1] == "Continuation" or line_array[1] == "Spawn":
#                     parent_task = line_array[5]
#                     parent_task_dic[parent_task] = 0
#                     print("parent_task: " + parent_task)
#                     print("parent_task_dic: ", end="")
#                     print(parent_task_dic)
#             elif line_array[0] == "CONTEXT_SWITCH:":
#                 scheduled_task = line_array[2]
#                 print("scheduled_task: " + scheduled_task)
#                 try:
#                     parent_task_dic[scheduled_task] += 1
#                     print("UNEXPECTED: parent got scheduled after last creation of a spawn and/or continuation task.")
#                 except:
#                      print("NORMAL: scheduled task is not a previous parent task of an await point")
#     except:
#         break

# print(parent_task_dic)
# # TODO: wakh through the parent_task_dic to print whether such case exist?