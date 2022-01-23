while True:
    try:
        line = input()
        print(line)
    except EOFERROR:
        break