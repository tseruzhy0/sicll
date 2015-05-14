/**
 * John Beres
 * Create a Linker Loader for the SICXE language
 * below is an examble of how to run the program
 * the starting address must be entered in as hex
 * sicll.exe 4000 prog1.txt prog2.txt prog3.txt
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace sicll
{
    class sicll
    {
        
        static void Main(string[] args)
        {
            //args 0 start addr, args 1 file1, args 2 file2, args 3 file3
            Assemble engageMrSulu = new Assemble(Int32.Parse(args[0],System.Globalization.NumberStyles.HexNumber), args[1], args[2], args[3]);
            engageMrSulu.PassOne();
            engageMrSulu.PassTwo();
            engageMrSulu.printSymTab();
            engageMrSulu.createOutput();
        }
    }

    class Assemble
    {
        int ProgAddr;
        SourceFile sFile;
        List<SymbolTable> symTab = new List<SymbolTable>();
        List<ProgTemplate> progTab = new List<ProgTemplate>();
        TRecords T_Records = new TRecords();
        MRecords M_Records = new MRecords();
        int firstExecutableInstruction = 0;
        TextWriter output = new StreamWriter("outfile.txt");

        //Get the parameters from the command line
        //take all 3 source files and put them into a single list file called sFile
        public Assemble(int addr, String one, String two, String three)
        {
            ProgAddr = addr;
            sFile = new SourceFile(one, two, three);
        }

        public void PassOne()
        {
            /**
             * The OffsetAcummulator keeps track of the offset after each program is parsed
             * This is used to help calculate the addresses later
             * The programIterator is an easy way to keep track of which program a var in the symbol table belongs to
             */
            int lineCounter = 0;
            String tempLine = "";
            String tempOne, tempTwo;
            String curProg = "";
            char[] tokens;
            int OffsetAccummulator = 0;
            int programIterator = 0;

            //Go through each line of the source files
            for(; lineCounter < sFile.getSize(); lineCounter++)
            {
                tempLine = sFile.getLine(lineCounter);

                //Pull out the header data which has the value of the progam name and the 
                //starting address along with its length or offset
                if(tempLine.StartsWith("H"))
                {
                    String progName = "";
                    int i = 0;
                    
                    tokens = tempLine.ToCharArray(1, tempLine.Length - 1);
                    while(tokens[i].CompareTo(' ') != 0)
                    {
                        progName = progName + tokens[i];    
                        i++;   
                    }

                    i++;
                    tempOne = "";
                    tempTwo = "";

                    for(int j = 0; j<6; j++, i++)
                    {
                        tempOne = tempOne + tokens[i];
                    }

                    for(int j = 0; j<6; j++, i++)
                    {
                        tempTwo = tempTwo + tokens[i];
                    }

                    //After the first program is executed start add the offset of the first program
                    //and each one as it is finished
                    if (programIterator > 0)
                    {
                        int temp = Int32.Parse(tempOne, System.Globalization.NumberStyles.HexNumber);
                        temp += OffsetAccummulator;
                        tempOne = temp.ToString("X");
                    }

                    //keep track of the programs as well as put its value in the symbol table
                    symTab.Add(new SymbolTable(progName, tempOne, programIterator));
                    progTab.Add(new ProgTemplate(progName, tempOne, tempTwo, OffsetAccummulator));

                    //get the current progam name to associate the m and t records
                    curProg = progName;
                }
                //Parse the D records to create the symbol table
                else if( tempLine.StartsWith("D"))
                {
                    tokens = tempLine.ToCharArray(1,tempLine.Length - 1);
                    int i = 0;
                    String symbol = "";
                    String location = "";
                    while(i < tokens.Length)
                    {
                        while(tokens[i].CompareTo(' ') != 0)
                        {
                            symbol = symbol + tokens[i];
                            i++;
                        }

                        i++;

                        if (tokens[i].CompareTo(' ') != 0)
                        {
                            for (int j = 0; j < 6; j++)
                            {
                                location = location + tokens[i];
                                i++;
                            }
                            //Keep track of which symbol belong to wich program to help in the later calculations
                            int temp = Int32.Parse(location, System.Globalization.NumberStyles.HexNumber) + OffsetAccummulator;
                            location = temp.ToString("X");
                            symTab.Add(new SymbolTable(symbol, location, programIterator));
                            
                            symbol = "";
                            location = "";
                        }
                    }
                }
                //Grab the T record and pass it to its corresponding class to handle it
                else if (tempLine.StartsWith("T"))
                {
                    T_Records.Add(curProg, tempLine, OffsetAccummulator);
                }
                //Grab the M record and pass it to its corresponding class to handle it
                else if (tempLine.StartsWith("M"))
                {
                    M_Records.Add(curProg, tempLine);
                }
                //When we hit an E record we know we have finished a program
                else if (tempLine.StartsWith("E"))
                {
                    //if the E record contains the first executable instruction grab it
                    if (tempLine.Length > 3)
                    {
                        firstExecutableInstruction = ProgAddr + Int32.Parse(tempLine.Substring(1), System.Globalization.NumberStyles.HexNumber) + OffsetAccummulator;    
                    }
                    
                    //Goto the next program and add the previous programs offset to the offset accumulator
                    programIterator++;
                    if (programIterator > 0)
                    {
                        OffsetAccummulator += Int32.Parse(progTab[programIterator - 1].getLength(), System.Globalization.NumberStyles.HexNumber);
                    }
                }
            }
        }

        public void PassTwo()
        {
            /**
             * Short and sweet here just pass the M records to the T record class to modify the T records
             */
            for(int i = 0; i < M_Records.getSize(); i++)
            {
                T_Records.modifyObjCode(M_Records.getProgName(i), M_Records.getStartingAddress(i), M_Records.getOffset(i), getSymbolVal(M_Records.getModValue(i)), M_Records.getAddition(i));
            }
        }

        //Print out the symbol table
        public void printSymTab()
        {
            output.WriteLine("Control\t" + "Symbol\t" + "Address\t" + "Length");
            output.WriteLine("Section\t" + "Name\t");
            output.WriteLine();

            int j = 0;   //Var for the Inner loop

            for (int i = 0; i < 3; i++)
            {
                output.WriteLine(progTab[i].getControlSection() + "\t\t" + (progTab[i].getOffset() + ProgAddr).ToString("X") + "\t" + progTab[i].getLength());
                j++;
                while (j < symTab.Count && i == symTab[j].getProgramPosition())
                {
                    output.WriteLine("\t" + symTab[j].getSymbolName() + "\t" + ((Int32.Parse(symTab[j].getAddress(), System.Globalization.NumberStyles.HexNumber)) + progTab[i].getOffset() + ProgAddr).ToString("X"));
                    j++;
                }
            }

            output.WriteLine();
        }

        //Output how the program would look in memory
        public void createOutput()
        {
            int T_Record_Start = T_Records.getRecordStart(0);
            int T_Record_End = ProgAddr;
            int Memory_Iterator = ProgAddr;
            int Memory_Stop;
            int T_Record_Size = T_Records.getRecordSize();
            int T_Record_Counter = 0;
            int Offset_Accumulator = ProgAddr;
            char[] objCode = T_Records.getTRecord(0);
            int objCodeCounter = 0;
            char printChar = 'x';

            //Make sure we start at a nice even number
            //for examble if we have the address 4006 it will subtract the 6 to get 4000 and another 16 to get 3FF0
            Memory_Iterator -= Int32.Parse(ProgAddr.ToString("X").ToCharArray().Last().ToString(), System.Globalization.NumberStyles.HexNumber);
            Memory_Iterator -= 16;

            //Get the last address in the program so we know when to stop checking inside the T records
            //end the program with a full offset of 16 x's
            for (int i = 0; i < progTab.Count; i++)
            {
                T_Record_End += Int32.Parse(progTab[i].getLength(), System.Globalization.NumberStyles.HexNumber);
            }
            Memory_Stop = T_Record_End + 16;

            //buffer before and after the program with x's and print out the significant stuff inbetween
            //Print one character at a time checking for the addresses of t records and printing them when the memory iterator gets to them
            while (Memory_Iterator < Memory_Stop)
            {
                output.Write(Memory_Iterator.ToString("X") + '\t');

                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        if (Memory_Iterator >= (T_Record_Start + (ProgAddr + T_Records.getOffset(T_Record_Counter))) && Memory_Iterator < T_Record_End)
                        {
                            printChar = objCode[objCodeCounter];
                            objCodeCounter++;
                        }
                        else if (Memory_Iterator < ProgAddr || Memory_Iterator >= T_Record_End)
                        {
                            printChar = 'x';
                        }
                        else if (Memory_Iterator >= ProgAddr && Memory_Iterator <= T_Record_End)
                        {
                            printChar = '.';
                        }

                        if (objCodeCounter >= objCode.Count() && T_Record_Counter + 1 < T_Record_Size)
                        {
                            T_Record_Counter++;
                            objCode = T_Records.getTRecord(T_Record_Counter);
                            T_Record_Start = T_Records.getRecordStart(T_Record_Counter);
                            objCodeCounter = 0;
                        }

                        //Why do i iterate when its odd becuase i want to iterate every two characters printed
                        //and since arrays are 0 indexed to get even counting you do it on an odd count.
                        if (j % 2 != 0)
                        {
                            Memory_Iterator++;
                        }

                        output.Write(printChar);
                    }
                    output.Write('\t');
                }
                output.WriteLine();
            }

            output.WriteLine();
            output.WriteLine("The program loaded at address: " + ProgAddr.ToString("X"));
            output.WriteLine("The programs first executable instruction starts at: " + firstExecutableInstruction.ToString("X"));
            output.Close();
        }

        //Gets the offset of the symbol or variable
        public int getSymbolVal(String search)
        {
            for (int i = 0; i < symTab.Count; i++)
            {
                if (symTab[i].getSymbolName().CompareTo(search) == 0)
                {
                    //get the value covnert from hex and add the progaddr to it then return it
                    return (ProgAddr + Int32.Parse(symTab[i].getAddress(), System.Globalization.NumberStyles.HexNumber));
                }
            }
            return -1;
        }
    }

    class MRecords
    {
        /**
         * Class for handling the M records
         */

        List<MRecordTemplate> record = new List<MRecordTemplate>();

        public void Add(String prog, String MRecord)
        {
            record.Add(new MRecordTemplate(prog, MRecord));
        }

        public int getSize()
        {
            return record.Count;
        }

        public int getStartingAddress(int i)
        {
            return record[i].getStartingAddress();
        }

        public int getOffset(int i)
        {
            return record[i].getOffset();
        }

        public String getModValue(int i)
        {
            return record[i].getModValue();
        }

        public String getProgName(int i)
        {
            return record[i].getProgName();
        }

        public bool getAddition(int i)
        {
            return record[i].getAddition();
        }
    }

    class MRecordTemplate
    {
        /**
         * This template will break down the m records into more managable parts for easy 
         * modification
         */
        String MRecord = "";
        char[] tokens;
        int startAddress;
        int offset;
        bool add;
        String modValue = "";
        String progName = "";

        public MRecordTemplate(String prog, String record)
        {
            progName = prog;
            MRecord = record;
            breakDownMRecord();
        }

        private void breakDownMRecord()
        {
            String temp = "";

            //Get rid of the M at the beginning
            tokens = MRecord.ToCharArray(1, MRecord.Length - 1);

            //First 6 characters are the starting address for modification
            for(int i = 0; i < 6; i++)
            {
                temp = temp + tokens[i];
            }

            startAddress = Int32.Parse(temp,System.Globalization.NumberStyles.HexNumber);

            //next two numbers are the offset from the starting address in half bytes
            temp = "";
            temp = temp + tokens[6];
            temp = temp + tokens[7];
            
            offset = Int32.Parse(temp,System.Globalization.NumberStyles.HexNumber);

            //next character is a + or -, true for plus and false for minus
            if(tokens[8].CompareTo('+') == 0)
            {
                add = true;
            }
            else
            {
                add = false;
            }

            temp = "";

            //The rest is the symbol or variable that will be used to modify the t record
            for(int i = 9; i < tokens.Length; i++)
            {
                temp = temp + tokens[i];
            }

            modValue = temp;
        }
        
        public bool getAddition()
        {
            return add;
        }

        public String getModValue()
        {
            return modValue;
        }

        public int getStartingAddress()
        {
            return startAddress;
        }
        
        public int getOffset()
        {
            return offset;
        }

        public String getProgName()
        {
            return progName;
        }
    }

    class TRecords
    {
        /**
         * Class for handling the T records
         */
        List<TRecordTemplate> record = new List<TRecordTemplate>();

        public void Add(String progName, String Trecord, int offset)
        {
            record.Add(new TRecordTemplate(progName, Trecord, offset));
        }
        
        //Modify the T records based off the pramaters from the M record
        public void modifyObjCode(String progName, int start, int offset, int modValue, bool add)
        {
            int[] range;
            char[] objCode;
            
            //For each T record in the program
            for(int i = 0; i < record.Count; i++)
            {
                //CHeck first to make sure the M record and T record are in the same program
                if(record[i].getProgName().CompareTo(progName) == 0)
                {
                    range = record[i].getRange();
                    
                    //If they are in the same program check to see if the m records is within the bounds of the t record
                    if(start >= range[0] && start <= range[1])
                    {
                        int charCount = (range[1] - range[0]) * 2;
                        int charStart = (start - range[0]) * 2;
                        int countTill = charStart + offset;
                        objCode = record[i].getObjCode();
                        String temp = "";
                        char[] putBack;
                        int modifcation;
                        int charTakenOut = 0;

                        //need to make sure we are grabbing to characters at a time
                        //if its an odd number need to grab one more to make it even
                        if(countTill % 2 != 0)
                        {
                            
                            countTill += 1;
                        }

                        //Pull out the objCode specified by the M record
                        for(int j = charStart; j < countTill; j++)
                        {
                            temp = temp + objCode[j];
                            charTakenOut++;
                        }
                        
                        modifcation = Int32.Parse(temp, System.Globalization.NumberStyles.HexNumber);
                        
                        //If its addition add the modification value if not subtract it
                        if(add)
                        {
                            modifcation += modValue;
                        }
                        else
                        {
                            modifcation -= modValue;
                        }
                        
                        //Time to put the object code back also if it has trailing 0's put them back
                        putBack = modifcation.ToString("X").PadLeft(6,'0').ToCharArray();
                        
                        //Put it back in revers order and only put back what we took out
                        int k = putBack.Length - 1;
                        for(int j = countTill - 1; j >= charStart && k > 0; j--, k--)
                        {
                            objCode[j] = putBack[k];     
                        }

                        record[i].setObjCode(objCode);
                    }
                }
            }        
        }

        public char[] getTRecord(int i)
        {
            return record[i].getObjCode();
        }

        public int getRecordStart(int i)
        {
            int[] temp = record[i].getRange();
            return temp[0];
        }

        public int getRecordSize()
        {
            return record.Count;
        }

        public int getOffset(int i)
        {
            return record[i].getOffset();
        }
    }
            

    class TRecordTemplate
    {
        /**
         * Breaks down the t record into more managable pieces
         */
        String tempLine;
        char[] tokens;
        char[] objCode;
        String[] objectCode = new String[15];
        String progName;
        int[] range = new int[2];
        String tempString = "";
        int offsetAccumulator;

        public TRecordTemplate(String prog, String input, int offset)
        {
            tempLine = input;
            progName = prog;
            offsetAccumulator = offset;
            breakDownRecord();
        }

        private void breakDownRecord()
        {
            //Dump the T at the begining
            tokens = tempLine.ToCharArray(1, tempLine.Length - 1);

            //First 6 characters will be the starting address of the T record
            for(int i = 0; i < 6; i++)
            {
                tempString = tempString + tokens[i];
            }
            range[0] = Int32.Parse(tempString, System.Globalization.NumberStyles.HexNumber);

            //Next two characters make up the length of the t record in hex
            tempString = "";
            tempString = tempString + tokens[6];
            tempString = tempString + tokens[7];

            //Create the range of the t record
            range[1] = range[0] + (Int32.Parse(tempString, System.Globalization.NumberStyles.HexNumber) * 2);

            tempString = "";

            //Snag the rest of the record and you have all your object code
            for (int i = 8; i < tokens.Length; i++)
            {
                tempString = tempString + tokens[i];
            }

            objCode = tempString.ToCharArray();
        }

        public char[] getObjCode()
        {
            return objCode;
        }

        public int[] getRange()
        {
            return range;
        }

        public String getProgName()
        {
            return progName;
        }

        public int getOffset()
        {
            return offsetAccumulator;
        }

        public void setObjCode(char[] modedObjCode)
        {
            objCode = modedObjCode;
        }
    }
            

    class SymbolTable
    {
        /**
         * Symbol Table Template for easy management
         */
        String SymbolName;
        String address;
        int programPosition;
        
        public SymbolTable(String SN, String a, int prog)
        {
            SymbolName = SN;
            address = a;
            programPosition = prog;
        }

        public String getSymbolName()
        {
            return SymbolName;
        }

        public String getAddress()
        {
            return address;
        }

        public int getProgramPosition()
        {
            return programPosition;
        }
    }

    class ProgTemplate
    {
        /**
         * Program Table Template for easy management
         */
        String ControlSection;
        String address;
        String length;
        int progOffset;

        public ProgTemplate(String CS, String a, String l, int offset)
        {
            ControlSection = CS;
            address = a;
            length = l;
            progOffset = offset;
        }

        public int getOffset()
        {
            return progOffset;
        }

        public String getControlSection()
        {
            return ControlSection;
        }

        public int getAddress()
        {
            return Int32.Parse(address,System.Globalization.NumberStyles.HexNumber);
        }

        public String getLength()
        {
            return length;
        }
    }

    class SourceTemplate
    {
        /**
         * A template for more easily managing the compined source files
         */
        String line;

        public SourceTemplate(String l)
        {
            line = l;
        }

        public String getLine()
        {
            return line;
        }
    }

    class SourceFile
    {
        /**
         * Get all the source files and combine them into one big list
         */
        String[] fileNames;
        List<SourceTemplate> input = new List<SourceTemplate>();
        public SourceFile(String one, String two, String three)
        {
            String[] temp = {one, two, three};
            fileNames = temp;
            buildSource();
        }

        private void buildSource()
        {
            for(int i = 0; i < fileNames.Length; i++)
            {
                TextReader fileInput = File.OpenText(fileNames[i]);

                while (fileInput.Peek() > -1)
                {
                    input.Add(new SourceTemplate(fileInput.ReadLine()));
                }
            }
        }

        public String getLine(int i)
        {
            return input[i].getLine();
        }

        public int getSize()
        {
            return input.Count;
        }

    }
}
