# Bencode (pronounced like Bee-encode) is the encoding used by the peer-to-peer file sharing system BitTorrent for storing and transmitting loosely structured data.[1]

It supports four different types of values:

byte strings,
integers,
lists(arrays), and
dictionaries (associative arrays).
Bencoding is most commonly used in torrent files, and as such is part of the BitTorrent specification. These metadata files are simply bencoded dictionaries.

Encoding Algorithm
Bencode uses ASCII characters as delimiters and digits to encode data structures in a simple and compact format.

Integers are encoded as i<base10 integer>e.


Examples:
Zero is encoded as i0e.
The number 42 is encoded as i42e.
Negative forty-two is encoded as i-42e.

Byte Strings are encoded as <length>:<contents>.
The length is the number of bytes in the string, encoded in base 10.
A colon (:) separates the length and the contents.

Examples:
An empty string is encoded as 0:.
The string "bencode" is encoded as 7:bencode.

Lists are encoded as l<elements>e.
Begins with l and ends with e.

Examples:
An empty list is encoded as le.
A list containing the string "bencode" and the integer -20 is encoded as l7:bencodei-20ee.

Dictionaries are encoded as d<pairs>e.
Begins with d and ends with e.
Contains key-value pairs.

Examples:
An empty dictionary is encoded as de.
A dictionary with keys "wiki" ? "bencode" and "meaning" ? 42 is encoded as d7:meaningi42e4:wiki7:bencodee.

# info 
-- what parse file contains 
-- get the info and encode it 

steps are the order by the command in code crafters 

# single file torrent multi file torent 

-- look how i add the info length for the multi file support 