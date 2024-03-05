// regrex to match phone number in the format of 123-456-7890
const phoneRegex = /^\d{3}-\d{3}-\d{4}$/;

// test phone number against the phone number  with console.log
console.log(phoneRegex.test('123-456-7890')); // true 
console.log(phoneRegex.test('123-456-789')); // false




